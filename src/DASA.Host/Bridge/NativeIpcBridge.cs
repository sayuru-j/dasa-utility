using System.Text.Json;
using System.Text.Json.Serialization;
using DASA.Host.Models;
using DASA.Host.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Forms = System.Windows.Forms;

namespace DASA.Host.Bridge;

public sealed class NativeIpcBridge
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly WebView2 _webView;
    private readonly SettingsService _settings;
    private readonly RuleEngineService _rules;
    private readonly FileWatcherService _watcher;
    private readonly FileProcessorService _processor;
    private readonly HistoryStore _history;
    private readonly TrayIconManager _tray;
    private readonly IWindowHost _window;
    private readonly RuleDiscoveryService _discovery;
    private bool _attached;
    private int _discoveryInFlight;

    public NativeIpcBridge(
        WebView2 webView,
        SettingsService settings,
        RuleEngineService rules,
        FileWatcherService watcher,
        FileProcessorService processor,
        HistoryStore history,
        TrayIconManager tray,
        IWindowHost window,
        RuleDiscoveryService discovery)
    {
        _webView = webView;
        _settings = settings;
        _rules = rules;
        _watcher = watcher;
        _processor = processor;
        _history = history;
        _tray = tray;
        _window = window;
        _discovery = discovery;
    }

    public void Attach()
    {
        if (_attached) return;
        _attached = true;

        _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        _processor.FileProcessed += (_, payload) =>
        {
            UiDispatcher.Invoke(() =>
            {
                Emit(IpcMessageTypes.FileProcessed, payload);
                _tray.NotifyFileProcessed(payload);
            });
        };
        _processor.MalwareDetected += (_, payload) =>
        {
            UiDispatcher.Invoke(() => Emit(IpcMessageTypes.MalwareDetected, payload));
        };
        _watcher.StatusChanged += (_, running) =>
        {
            UiDispatcher.Invoke(() => Emit(IpcMessageTypes.WatcherStatusChanged, new WatcherStatusPayload
            {
                Monitoring = running && _settings.Current.MonitoringEnabled,
                WatchFolder = _watcher.WatchFolder
            }));
        };
    }

    public void EmitWindowState()
    {
        Emit(IpcMessageTypes.WindowStateChanged, new WindowStatePayload
        {
            IsMaximized = _window.IsMaximized
        });
    }

    public void EmitState()
    {
        var snapshot = new StateSnapshotPayload
        {
            Settings = _settings.ToViewModel(),
            Rules = _rules.GetRules().ToList(),
            History = _history.GetRecent(),
            QuarantineEvents = _history.GetQuarantineEvents()
        };
        Emit(IpcMessageTypes.StateSnapshot, snapshot);
    }

    public void Emit(string type, object? payload, string? requestId = null)
    {
        if (_webView.CoreWebView2 is null) return;

        var envelope = new Dictionary<string, object?>
        {
            ["type"] = type,
            ["requestId"] = requestId,
            ["payload"] = payload
        };

        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        _webView.CoreWebView2.PostWebMessageAsJson(json);
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.WebMessageAsJson;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString() ?? string.Empty;
            var requestId = root.TryGetProperty("requestId", out var rid) ? rid.GetString() : null;
            var payloadElement = root.TryGetProperty("payload", out var p) ? p : (JsonElement?)null;

            switch (type)
            {
                case IpcMessageTypes.GetState:
                    EmitState();
                    break;

                case IpcMessageTypes.SaveRule:
                {
                    var rule = payloadElement?.Deserialize<AutomationRule>(JsonOptions);
                    if (rule is not null)
                    {
                        _rules.SaveRule(rule);
                        Emit(IpcMessageTypes.RulesUpdated, _rules.GetRules(), requestId);
                    }
                    break;
                }

                case IpcMessageTypes.DeleteRule:
                {
                    var id = payloadElement?.GetProperty("id").GetString();
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        _rules.DeleteRule(id);
                        Emit(IpcMessageTypes.RulesUpdated, _rules.GetRules(), requestId);
                    }
                    break;
                }

                case IpcMessageTypes.ReorderRules:
                {
                    var reorder = payloadElement?.Deserialize<ReorderRulesPayload>(JsonOptions);
                    if (reorder is not null)
                    {
                        _rules.Reorder(reorder.OrderedIds);
                        Emit(IpcMessageTypes.RulesUpdated, _rules.GetRules(), requestId);
                    }
                    break;
                }

                case IpcMessageTypes.UpdateSettings:
                {
                    var update = payloadElement?.Deserialize<SettingsUpdatePayload>(JsonOptions);
                    if (update is not null)
                    {
                        var applied = _settings.Apply(update);
                        ApplyMonitoring(applied);
                        Emit(IpcMessageTypes.SettingsUpdated, _settings.ToViewModel(), requestId);
                        Emit(IpcMessageTypes.WatcherStatusChanged, new WatcherStatusPayload
                        {
                            Monitoring = _watcher.IsRunning && applied.MonitoringEnabled,
                            WatchFolder = applied.WatchFolder
                        });
                    }
                    break;
                }

                case IpcMessageTypes.SetMonitoring:
                {
                    var enabled = payloadElement?.GetProperty("enabled").GetBoolean() ?? false;
                    var applied = _settings.Apply(new SettingsUpdatePayload { MonitoringEnabled = enabled });
                    ApplyMonitoring(applied);
                    Emit(IpcMessageTypes.WatcherStatusChanged, new WatcherStatusPayload
                    {
                        Monitoring = _watcher.IsRunning && applied.MonitoringEnabled,
                        WatchFolder = applied.WatchFolder
                    }, requestId);
                    break;
                }

                case IpcMessageTypes.TriggerManualScan:
                    _ = Task.Run(async () =>
                    {
                        await _watcher.ScanExistingAsync().ConfigureAwait(false);
                    });
                    Emit(IpcMessageTypes.WatcherStatusChanged, new WatcherStatusPayload
                    {
                        Monitoring = _watcher.IsRunning,
                        WatchFolder = _watcher.WatchFolder
                    }, requestId);
                    break;

                case IpcMessageTypes.UndoMove:
                {
                    var undo = payloadElement?.Deserialize<UndoMovePayload>(JsonOptions);
                    if (undo is null)
                    {
                        Emit(IpcMessageTypes.Error, new { message = "Missing undo token." }, requestId);
                        break;
                    }

                    if (_processor.TryUndo(undo.UndoToken, out var message))
                    {
                        EmitState();
                        _tray.ShowBalloon("Undo complete", message ?? "File restored.", Forms.ToolTipIcon.Info);
                    }
                    else
                    {
                        Emit(IpcMessageTypes.Error, new { message = message ?? "Undo failed." }, requestId);
                    }
                    break;
                }

                case IpcMessageTypes.PickFolder:
                {
                    var pick = payloadElement?.Deserialize<PickFolderPayload>(JsonOptions) ?? new PickFolderPayload();
                    var dialog = new Forms.FolderBrowserDialog
                    {
                        Description = pick.Purpose switch
                        {
                            "sort" => "Choose default sort root",
                            "rule" => "Choose rule destination folder",
                            _ => "Choose watch folder"
                        },
                        UseDescriptionForTitle = true
                    };

                    if (dialog.ShowDialog() == Forms.DialogResult.OK)
                    {
                        if (pick.Purpose == "rule")
                        {
                            Emit(IpcMessageTypes.FolderPicked, new FolderPickedPayload
                            {
                                Path = dialog.SelectedPath,
                                Purpose = "rule"
                            }, requestId);
                            break;
                        }

                        var update = pick.Purpose == "sort"
                            ? new SettingsUpdatePayload { DefaultSortRoot = dialog.SelectedPath }
                            : new SettingsUpdatePayload { WatchFolder = dialog.SelectedPath };

                        var applied = _settings.Apply(update);
                        ApplyMonitoring(applied);
                        Emit(IpcMessageTypes.SettingsUpdated, _settings.ToViewModel(), requestId);
                    }
                    break;
                }

                case IpcMessageTypes.WindowMinimize:
                    _window.Minimize();
                    break;

                case IpcMessageTypes.WindowMaximize:
                    _window.ToggleMaximize();
                    EmitWindowState();
                    break;

                case IpcMessageTypes.WindowClose:
                    _window.HideToTray();
                    break;

                case IpcMessageTypes.WindowDrag:
                    _window.BeginDragMove();
                    break;

                case IpcMessageTypes.DiscoverRules:
                    if (Interlocked.CompareExchange(ref _discoveryInFlight, 1, 0) != 0)
                    {
                        Emit(IpcMessageTypes.Error, new { message = "Rule discovery is already running." }, requestId);
                        break;
                    }

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var result = await _discovery.DiscoverAsync(
                                (phase, detail) =>
                                {
                                    UiDispatcher.Invoke(() =>
                                        Emit(IpcMessageTypes.DiscoverRulesProgress, new DiscoverRulesProgressPayload
                                        {
                                            Phase = phase,
                                            Detail = detail
                                        }));
                                }).ConfigureAwait(false);

                            UiDispatcher.Invoke(() =>
                                Emit(IpcMessageTypes.RulesDiscovered, new RulesDiscoveredPayload
                                {
                                    Summary = result.Summary,
                                    FoldersScanned = result.FoldersScanned,
                                    Rules = result.Rules.Select(r => new DiscoveredRuleDto
                                    {
                                        Id = r.Id,
                                        Name = r.Name,
                                        Extension = r.Extension,
                                        NameContains = r.NameContains,
                                        DestinationFolder = r.DestinationFolder,
                                        Confidence = r.Confidence,
                                        Reason = r.Reason
                                    }).ToList()
                                }, requestId));
                        }
                        catch (Exception ex)
                        {
                            UiDispatcher.Invoke(() =>
                                Emit(IpcMessageTypes.Error, new { message = ex.Message }, requestId));
                        }
                        finally
                        {
                            Interlocked.Exchange(ref _discoveryInFlight, 0);
                        }
                    });
                    break;

                case IpcMessageTypes.ApplyDiscoveredRules:
                {
                    var apply = payloadElement?.Deserialize<ApplyDiscoveredRulesPayload>(JsonOptions);
                    if (apply?.Rules is null || apply.Rules.Count == 0) break;

                    var startPriority = _rules.GetRules().Count;
                    for (var i = 0; i < apply.Rules.Count; i++)
                    {
                        var dto = apply.Rules[i];
                        _rules.SaveRule(new AutomationRule
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            Name = dto.Name,
                            Enabled = true,
                            Priority = startPriority + i,
                            Extension = dto.Extension,
                            NameContains = dto.NameContains,
                            DestinationFolder = dto.DestinationFolder
                        });
                    }

                    Emit(IpcMessageTypes.RulesUpdated, _rules.GetRules(), requestId);
                    break;
                }

                case IpcMessageTypes.ClearActivity:
                    _history.ClearActivity();
                    EmitState();
                    break;
            }
        }
        catch (Exception ex)
        {
            Emit(IpcMessageTypes.Error, new { message = ex.Message });
        }
    }

    private void ApplyMonitoring(AppSettings settings)
    {
        if (settings.MonitoringEnabled)
        {
            if (!_watcher.IsRunning ||
                !string.Equals(_watcher.WatchFolder, settings.WatchFolder, StringComparison.OrdinalIgnoreCase))
            {
                _watcher.Start(settings.WatchFolder);
            }
        }
        else
        {
            _watcher.Stop();
        }
    }
}
