using System.Collections.Concurrent;
using System.IO;

namespace DASA.Host.Services;

/// <summary>
/// Monitors the Downloads folder, ignoring temp browser extensions, and waits until
/// files are fully written and unlocked before raising <see cref="FileReady"/>.
/// </summary>
public sealed class FileWatcherService : IDisposable
{
    private static readonly HashSet<string> TempExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".crdownload", ".part", ".tmp", ".download", ".partial", ".opdownload", ".!ut", ".bc!"
    };

    private readonly Func<int> _getWaitTimeMinutes;
    private readonly ConcurrentDictionary<string, byte> _inflight = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    public FileWatcherService(Func<int> getWaitTimeMinutes)
    {
        _getWaitTimeMinutes = getWaitTimeMinutes;
    }

    public string WatchFolder { get; private set; } = string.Empty;
    public bool IsRunning => _watcher is { EnableRaisingEvents: true };

    public event EventHandler<string>? FileReady;
    public event EventHandler<bool>? StatusChanged;

    public void Start(string folder)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        StopInternal();

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        WatchFolder = folder;
        _watcher = new FileSystemWatcher(folder)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            InternalBufferSize = 64 * 1024
        };

        _watcher.Created += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += (_, _) =>
        {
            try
            {
                if (_watcher is not null)
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.EnableRaisingEvents = true;
                }
            }
            catch
            {
                // ignored
            }
        };

        _watcher.EnableRaisingEvents = true;
        StatusChanged?.Invoke(this, true);
    }

    public void Stop()
    {
        StopInternal();
        StatusChanged?.Invoke(this, false);
    }

    public async Task ScanExistingAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(WatchFolder) || !Directory.Exists(WatchFolder))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(WatchFolder))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ShouldIgnore(path)) continue;
            _ = ProcessWhenReadyAsync(path, applyWaitTime: false);
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnore(e.FullPath)) return;
        _ = ProcessWhenReadyAsync(e.FullPath, applyWaitTime: true);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (ShouldIgnore(e.FullPath)) return;
        _ = ProcessWhenReadyAsync(e.FullPath, applyWaitTime: true);
    }

    private static bool ShouldIgnore(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;
        if (Directory.Exists(path)) return true;

        var ext = Path.GetExtension(path);
        if (TempExtensions.Contains(ext)) return true;

        var name = Path.GetFileName(path);
        if (name.StartsWith('~') || name.StartsWith('.')) return true;

        return false;
    }

    private async Task ProcessWhenReadyAsync(string path, bool applyWaitTime)
    {
        if (!_inflight.TryAdd(path, 0))
        {
            return;
        }

        try
        {
            const int maxAttempts = 40;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 + attempt * 100)).ConfigureAwait(false);

                if (!File.Exists(path) || ShouldIgnore(path))
                {
                    return;
                }

                if (!IsFileReady(path))
                {
                    continue;
                }

                var size1 = new FileInfo(path).Length;
                await Task.Delay(400).ConfigureAwait(false);
                if (!File.Exists(path)) return;
                var size2 = new FileInfo(path).Length;
                if (size1 != size2 || !IsFileReady(path))
                {
                    continue;
                }

                if (applyWaitTime)
                {
                    var waitMinutes = Math.Max(0, _getWaitTimeMinutes());
                    if (waitMinutes > 0)
                    {
                        await Task.Delay(TimeSpan.FromMinutes(waitMinutes)).ConfigureAwait(false);
                        if (!File.Exists(path) || ShouldIgnore(path) || !IsFileReady(path))
                        {
                            return;
                        }
                    }
                }

                FileReady?.Invoke(this, path);
                return;
            }
        }
        finally
        {
            _inflight.TryRemove(path, out _);
        }
    }

    private static bool IsFileReady(string path)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void StopInternal()
    {
        if (_watcher is null) return;
        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnChanged;
        _watcher.Changed -= OnChanged;
        _watcher.Renamed -= OnRenamed;
        _watcher.Dispose();
        _watcher = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopInternal();
    }
}
