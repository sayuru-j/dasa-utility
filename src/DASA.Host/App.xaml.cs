using System.Windows;
using DASA.Host.Services;

namespace DASA.Host;

public partial class App : System.Windows.Application
{
    private AppServices? _services;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _services = new AppServices(ShowMainWindow, ExitApplication);
        _mainWindow = new MainWindow(_services);
        _mainWindow.Show();
    }

    private void ShowMainWindow()
    {
        UiDispatcher.Invoke(() =>
        {
            if (_mainWindow is null) return;
            if (!_mainWindow.IsVisible) _mainWindow.Show();
            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }

            _mainWindow.Activate();
            _mainWindow.Topmost = true;
            _mainWindow.Topmost = false;
            _mainWindow.Focus();
        });
    }

    private void ExitApplication()
    {
        UiDispatcher.Invoke(() =>
        {
            _services?.Dispose();
            _mainWindow?.ForceClose();
            Shutdown();
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}

public sealed class AppServices : IDisposable
{
    public SettingsService Settings { get; }
    public RuleEngineService Rules { get; }
    public HistoryStore History { get; }
    public AmsiScanner Amsi { get; }
    public GeminiAiClient Gemini { get; }
    public FileWatcherService Watcher { get; }
    public FileProcessorService Processor { get; }
    public TrayIconManager Tray { get; }
    public RuleDiscoveryService Discovery { get; }
    public MoveNotificationOverlayService MoveNotifications { get; }

    public AppServices(Action showWindow, Action exitApp)
    {
        Settings = new SettingsService();
        Rules = new RuleEngineService(Settings.DataDirectory);
        History = new HistoryStore(Settings.DataDirectory);
        Amsi = new AmsiScanner();
        Gemini = new GeminiAiClient();
        Discovery = new RuleDiscoveryService(Settings, Gemini);
        Watcher = new FileWatcherService(() => Settings.Current.WaitTimeMinutes);
        Processor = new FileProcessorService(Settings, Amsi, Rules, Gemini, History);
        Tray = new TrayIconManager(showWindow, exitApp);
        MoveNotifications = new MoveNotificationOverlayService(token => Processor.TryUndo(token, out _));

        Watcher.FileReady += (_, path) =>
        {
            _ = Processor.ProcessAsync(path);
        };

        var current = Settings.Current;
        if (current.MonitoringEnabled)
        {
            Watcher.Start(current.WatchFolder);
        }
    }

    public void Dispose()
    {
        Watcher.Dispose();
        Amsi.Dispose();
        Gemini.Dispose();
        MoveNotifications.Dispose();
        Tray.Dispose();
    }
}
