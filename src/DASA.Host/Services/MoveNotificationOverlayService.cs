using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DASA.Host.Models;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using Size = System.Windows.Size;

namespace DASA.Host.Services;

public sealed class MoveNotificationOverlayService : IDisposable
{
    private const int ToastWidth = 340;
    private const int ScreenMargin = 20;
    private const int MaxToasts = 5;
    private const int AutoDismissMs = 8000;
    private const int ProgressTickMs = 40;

    private static readonly FontFamily UiFont = new("Segoe UI");
    private static readonly FontFamily MonoFont = new("IBM Plex Mono, Consolas");

    private static readonly Color SurfaceAlt = Color.FromRgb(17, 17, 17);
    private static readonly Color SurfaceElevated = Color.FromRgb(22, 22, 22);
    private static readonly Color Surface = Color.FromRgb(10, 10, 10);
    private static readonly Color Stroke = Color.FromRgb(30, 30, 30);
    private static readonly Color StrokeStrong = Color.FromRgb(42, 42, 42);
    private static readonly Color Accent = Color.FromRgb(215, 25, 33);
    private static readonly Color DangerBg = Color.FromRgb(26, 10, 10);
    private static readonly Color TextPrimary = Color.FromRgb(245, 245, 245);
    private static readonly Color TextSecondary = Color.FromRgb(136, 136, 136);
    private static readonly Color TextTertiary = Color.FromRgb(85, 85, 85);
    private static readonly Color Success = Color.FromRgb(74, 222, 128);

    private readonly Func<string, bool> _tryUndo;
    private readonly List<ToastEntry> _toasts = [];
    private Window? _hostWindow;
    private StackPanel? _stackPanel;
    private bool _disposed;

    public MoveNotificationOverlayService(Func<string, bool> tryUndo)
    {
        _tryUndo = tryUndo;
    }

    public void Show(FileProcessedPayload payload)
    {
        if (_disposed) return;

        UiDispatcher.Invoke(() =>
        {
            _toasts.Insert(0, new ToastEntry(payload));
            while (_toasts.Count > MaxToasts)
            {
                RemoveToast(_toasts[^1], notify: false);
            }

            EnsureWindow();
            RenderToasts();
            UpdateWindowVisibility();
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UiDispatcher.Invoke(() =>
        {
            foreach (var toast in _toasts.ToList())
            {
                toast.Timer.Stop();
                toast.ProgressTimer.Stop();
            }

            _toasts.Clear();
            _hostWindow?.Close();
            _hostWindow = null;
            _stackPanel = null;
        });
    }

    private void EnsureWindow()
    {
        if (_hostWindow is not null) return;

        _stackPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(ScreenMargin, ScreenMargin, ScreenMargin, ScreenMargin)
        };

        var root = new Grid
        {
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        root.Children.Add(_stackPanel);

        _hostWindow = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            Topmost = true,
            ResizeMode = ResizeMode.NoResize,
            ShowActivated = false,
            Focusable = false,
            Content = root
        };

        _hostWindow.SourceInitialized += (_, _) => ApplyOverlayStyles(_hostWindow);
        _hostWindow.Deactivated += (_, _) =>
        {
            if (_hostWindow is not null)
            {
                _hostWindow.Topmost = true;
            }
        };
    }

    private void RenderToasts()
    {
        if (_stackPanel is null) return;

        _stackPanel.Children.Clear();

        foreach (var entry in _toasts)
        {
            _stackPanel.Children.Add(CreateToastCard(entry));
        }

        UpdateWindowBounds();
    }

    private UIElement CreateToastCard(ToastEntry entry)
    {
        var payload = entry.Payload;
        var isQuarantine = payload.Quarantined;

        var card = new Border
        {
            Width = ToastWidth,
            Margin = new Thickness(0, 0, 0, 12),
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(isQuarantine ? DangerBg : SurfaceAlt),
            BorderBrush = new SolidColorBrush(isQuarantine ? Accent : Stroke),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 28,
                ShadowDepth = 0,
                Opacity = 0.55,
                Color = Colors.Black
            }
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var body = new StackPanel { Margin = new Thickness(16, 14, 16, 14) };

        // Header: dot + label .............. dismiss
        var header = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var labelRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        labelRow.Children.Add(new Border
        {
            Width = 6,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(isQuarantine ? Accent : Success),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        labelRow.Children.Add(new TextBlock
        {
            Text = isQuarantine ? "QUARANTINED" : "FILE SORTED",
            FontFamily = MonoFont,
            FontSize = 10,
            Foreground = new SolidColorBrush(isQuarantine ? Accent : TextTertiary),
            VerticalAlignment = VerticalAlignment.Center
        });

        var dismiss = CreateIconButton("×", () => RemoveToast(entry));
        Grid.SetColumn(labelRow, 0);
        Grid.SetColumn(dismiss, 2);
        header.Children.Add(labelRow);
        header.Children.Add(dismiss);
        body.Children.Add(header);

        // File name
        body.Children.Add(new TextBlock
        {
            Text = payload.FileName,
            FontFamily = UiFont,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(TextPrimary),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Destination path box
        var pathBox = new Border
        {
            Background = new SolidColorBrush(Surface),
            BorderBrush = new SolidColorBrush(Stroke),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 10)
        };
        pathBox.Child = new TextBlock
        {
            Text = $"→ {ShortDestination(payload.DestinationPath)}",
            FontFamily = MonoFont,
            FontSize = 11,
            Foreground = new SolidColorBrush(TextSecondary),
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        };
        body.Children.Add(pathBox);

        // Source badge + actions
        var footer = new Grid();
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        footer.Children.Add(CreateSourceBadge(SourceLabel(payload.Source)));

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (!string.IsNullOrWhiteSpace(payload.UndoToken) && !isQuarantine)
        {
            var undoButton = CreateGhostButton("Undo", () =>
            {
                if (_tryUndo(payload.UndoToken!))
                {
                    RemoveToast(entry);
                }
            });
            undoButton.Margin = new Thickness(0, 0, 8, 0);
            actions.Children.Add(undoButton);
        }

        actions.Children.Add(CreatePrimaryButton("Open folder", () => ExplorerHelper.Open(payload.DestinationPath)));

        Grid.SetColumn(actions, 2);
        footer.Children.Add(actions);
        body.Children.Add(footer);

        // Auto-dismiss progress bar
        var progressTrack = new Border
        {
            Height = 2,
            Background = new SolidColorBrush(Stroke),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        var progressFill = new Border
        {
            Height = 2,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(isQuarantine ? Accent : TextTertiary),
            Width = ToastWidth
        };
        progressTrack.Child = progressFill;

        Grid.SetRow(body, 0);
        Grid.SetRow(progressTrack, 1);
        Grid.SetRowSpan(body, 1);
        root.Children.Add(body);
        root.Children.Add(progressTrack);

        card.Child = root;

        entry.Timer.Stop();
        entry.ProgressTimer.Stop();
        entry.ProgressFill = progressFill;
        entry.Progress = 1.0;

        entry.Timer = CreateDismissTimer(entry);
        entry.ProgressTimer = CreateProgressTimer(entry);
        entry.Timer.Start();
        entry.ProgressTimer.Start();

        return card;
    }

    private static Border CreateSourceBadge(string label)
    {
        return new Border
        {
            Background = new SolidColorBrush(SurfaceElevated),
            BorderBrush = new SolidColorBrush(StrokeStrong),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = label,
                FontFamily = MonoFont,
                FontSize = 10,
                Foreground = new SolidColorBrush(TextSecondary)
            }
        };
    }

    private static Button CreatePrimaryButton(string label, Action onClick)
    {
        var button = new Button
        {
            Content = label,
            Cursor = System.Windows.Input.Cursors.Hand,
            FontFamily = UiFont,
            FontSize = 12,
            FontWeight = FontWeights.Medium,
            Background = new SolidColorBrush(TextPrimary),
            Foreground = new SolidColorBrush(Surface),
            BorderBrush = new SolidColorBrush(TextPrimary),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6, 12, 6)
        };

        ApplyPrimaryHover(button);
        button.Click += (_, _) => onClick();
        return button;
    }

    private static Button CreateGhostButton(string label, Action onClick)
    {
        var button = new Button
        {
            Content = label,
            Cursor = System.Windows.Input.Cursors.Hand,
            FontFamily = UiFont,
            FontSize = 12,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(TextSecondary),
            BorderBrush = new SolidColorBrush(Stroke),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6, 12, 6)
        };

        ApplyGhostHover(button);
        button.Click += (_, _) => onClick();
        return button;
    }

    private static Button CreateIconButton(string label, Action onClick)
    {
        var button = new Button
        {
            Content = label,
            Cursor = System.Windows.Input.Cursors.Hand,
            FontFamily = UiFont,
            FontSize = 16,
            FontWeight = FontWeights.Light,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(TextTertiary),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 0, 0, 0),
            MinWidth = 24,
            MinHeight = 24,
            VerticalAlignment = VerticalAlignment.Top
        };

        button.MouseEnter += (_, _) => button.Foreground = new SolidColorBrush(TextPrimary);
        button.MouseLeave += (_, _) => button.Foreground = new SolidColorBrush(TextTertiary);
        button.Click += (_, _) => onClick();
        return button;
    }

    private static void ApplyPrimaryHover(Button button)
    {
        var normalBg = new SolidColorBrush(TextPrimary);
        var hoverBg = new SolidColorBrush(Color.FromRgb(224, 224, 224));

        button.MouseEnter += (_, _) => button.Background = hoverBg;
        button.MouseLeave += (_, _) => button.Background = normalBg;
    }

    private static void ApplyGhostHover(Button button)
    {
        var normalBg = Brushes.Transparent;
        var hoverBg = new SolidColorBrush(SurfaceElevated);
        var normalFg = new SolidColorBrush(TextSecondary);
        var hoverFg = new SolidColorBrush(TextPrimary);
        var normalBorder = new SolidColorBrush(Stroke);
        var hoverBorder = new SolidColorBrush(StrokeStrong);

        button.MouseEnter += (_, _) =>
        {
            button.Background = hoverBg;
            button.Foreground = hoverFg;
            button.BorderBrush = hoverBorder;
        };
        button.MouseLeave += (_, _) =>
        {
            button.Background = normalBg;
            button.Foreground = normalFg;
            button.BorderBrush = normalBorder;
        };
    }

    private DispatcherTimer CreateDismissTimer(ToastEntry entry)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(AutoDismissMs) };
        timer.Tick += (_, _) => RemoveToast(entry);
        return timer;
    }

    private DispatcherTimer CreateProgressTimer(ToastEntry entry)
    {
        var started = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ProgressTickMs) };
        timer.Tick += (_, _) =>
        {
            var elapsed = (DateTime.UtcNow - started).TotalMilliseconds;
            entry.Progress = Math.Max(0, 1 - elapsed / AutoDismissMs);
            if (entry.ProgressFill is not null)
            {
                entry.ProgressFill.Width = ToastWidth * entry.Progress;
            }

            if (entry.Progress <= 0)
            {
                timer.Stop();
            }
        };
        return timer;
    }

    private void RemoveToast(ToastEntry entry, bool notify = true)
    {
        if (!_toasts.Remove(entry)) return;

        entry.Timer.Stop();
        entry.ProgressTimer.Stop();

        if (!notify) return;

        RenderToasts();
        UpdateWindowVisibility();
    }

    private void UpdateWindowBounds()
    {
        if (_hostWindow is null || _stackPanel is null) return;

        _stackPanel.Measure(new Size(ToastWidth + ScreenMargin * 2, double.PositiveInfinity));
        var contentHeight = _stackPanel.DesiredSize.Height;
        var workArea = SystemParameters.WorkArea;

        _hostWindow.Width = ToastWidth + ScreenMargin * 2;
        _hostWindow.Height = Math.Min(contentHeight, workArea.Height - ScreenMargin * 2);
        _hostWindow.Left = workArea.Right - _hostWindow.Width;
        _hostWindow.Top = workArea.Top;
    }

    private void UpdateWindowVisibility()
    {
        if (_hostWindow is null) return;

        if (_toasts.Count == 0)
        {
            _hostWindow.Hide();
            return;
        }

        if (!_hostWindow.IsVisible)
        {
            _hostWindow.Show();
        }

        _hostWindow.Topmost = true;
    }

    private static void ApplyOverlayStyles(Window window)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero) return;

        const int gwlExstyle = -20;
        const int wsExToolWindow = 0x00000080;
        const int wsExNoActivate = 0x08000000;

        var style = GetWindowLong(helper.Handle, gwlExstyle);
        SetWindowLong(helper.Handle, gwlExstyle, style | wsExToolWindow | wsExNoActivate);
    }

    private static string SourceLabel(string source) => source switch
    {
        "amsi" => "AMSI",
        "rule" => "Rule",
        "gemini" => "Gemini",
        _ => "Default"
    };

    private static string ShortDestination(string path)
    {
        var normalized = path.TrimEnd('\\', '/');
        var parts = normalized.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return path;

        var fileName = parts[^1];
        if (parts.Length == 1) return fileName;

        var parent = parts[^2];
        return $"{parent}/{fileName}";
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private sealed class ToastEntry
    {
        public ToastEntry(FileProcessedPayload payload)
        {
            Payload = payload;
        }

        public FileProcessedPayload Payload { get; }
        public DispatcherTimer Timer { get; set; } = new();
        public DispatcherTimer ProgressTimer { get; set; } = new();
        public Border? ProgressFill { get; set; }
        public double Progress { get; set; } = 1;
    }
}
