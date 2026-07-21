using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using DASA.Host.Bridge;
using DASA.Host.Services;
using Microsoft.Web.WebView2.Core;
using MessageBox = System.Windows.MessageBox;

namespace DASA.Host;

public partial class MainWindow : Window, IWindowHost
{
    private readonly AppServices _services;
    private NativeIpcBridge? _bridge;
    private bool _forceClose;

    public MainWindow(AppServices services)
    {
        _services = services;
        InitializeComponent();
        ApplyWindowIcon();
        WindowMaximizeHelper.EnableWorkAreaMaximize(this);
        Loaded += OnLoaded;
        Closing += OnClosing;
        StateChanged += OnStateChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowChrome.GetWindowChrome(this) is { } chrome)
        {
            chrome.CornerRadius = WindowState == WindowState.Maximized ? new CornerRadius(0) : new CornerRadius(8);
        }

        WindowStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool IsMaximized => WindowState == WindowState.Maximized;

    public event EventHandler? WindowStateChanged;

    public void Minimize() => WindowState = WindowState.Minimized;

    public void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    public void HideToTray()
    {
        Hide();
        _services.Tray.ShowBalloon("D.A.S.A", "Minimized to tray. Still monitoring downloads.");
    }

    public void BeginDragMove() => WindowDragHelper.DragMoveWindow(this);

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    private void ApplyWindowIcon()
    {
        using var icon = AppIconHelper.LoadAppIcon();
        if (icon.Handle == IntPtr.Zero)
        {
            return;
        }

        base.Icon = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LoadingStatus.Text = "Initializing WebView2 runtime…";
            WebView.DefaultBackgroundColor = Color.FromArgb(255, 10, 10, 10);

            var userData = Path.Combine(_services.Settings.DataDirectory, "WebView2");
            Directory.CreateDirectory(userData);

            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
            await WebView.EnsureCoreWebView2Async(env);

            var core = WebView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.NavigationStarting += OnExternalNavigationStarting;
            core.NewWindowRequested += OnNewWindowRequested;

            _bridge = new NativeIpcBridge(
                WebView,
                _services.Settings,
                _services.Rules,
                _services.Watcher,
                _services.Processor,
                _services.History,
                _services.Tray,
                _services.MoveNotifications,
                this,
                _services.Discovery);
            _bridge.Attach();

            WindowStateChanged += (_, _) => _bridge?.EmitWindowState();

            core.NavigationCompleted += OnNavigationCompleted;
            core.ProcessFailed += OnProcessFailed;

            var plan = await WebViewUiLoader.ResolveAsync();
            if (plan.DistFolder is not null)
            {
                WebViewUiLoader.ApplyVirtualHostMapping(core, plan.DistFolder);
            }

            LoadingStatus.Text = $"Loading UI ({plan.Source})…";
            core.Navigate(plan.NavigateUrl);
        }
        catch (Exception ex)
        {
            LoadingStatus.Text = $"Failed to start WebView2: {ex.Message}";
            MessageBox.Show(
                $"WebView2 failed to initialize.\n\n{ex.Message}\n\nInstall the Evergreen WebView2 Runtime and try again.",
                "D.A.S.A",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnExternalNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (!ShouldOpenExternally(args.Uri))
        {
            return;
        }

        args.Cancel = true;
        TryOpenExternalUrl(args.Uri);
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        args.Handled = true;
        TryOpenExternalUrl(args.Uri);
    }

    private static bool ShouldOpenExternally(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (parsed.Scheme is not ("http" or "https"))
        {
            return false;
        }

        var host = parsed.Host;
        return !host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            && !host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            && !host.Equals(WebViewUiLoader.VirtualHostName, StringComparison.OrdinalIgnoreCase);
    }

    private static void TryOpenExternalUrl(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch
        {
            // ignored
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (args.IsSuccess)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            _bridge?.EmitState();
            _bridge?.EmitWindowState();
            return;
        }

        LoadingOverlay.Visibility = Visibility.Visible;
        LoadingStatus.Text =
            $"UI failed to load ({args.WebErrorStatus}). " +
            "Start Vite with: cd src\\dasa-ui && npm run dev";
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs args)
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        LoadingStatus.Text = $"WebView2 renderer crashed ({args.ProcessFailedKind}). Reloading…";

        try
        {
            WebView.CoreWebView2?.Reload();
        }
        catch
        {
            // ignored
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_forceClose)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }
}
