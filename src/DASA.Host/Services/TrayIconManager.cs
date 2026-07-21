using System.Windows;
using DASA.Host.Models;
using Application = System.Windows.Application;
using Forms = System.Windows.Forms;

namespace DASA.Host.Services;

public sealed class TrayIconManager : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Action _showWindow;
    private readonly Action _exitApp;
    private bool _disposed;

    public TrayIconManager(Action showWindow, Action exitApp)
    {
        _showWindow = showWindow;
        _exitApp = exitApp;

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open D.A.S.A", null, (_, _) => _showWindow());
        menu.Items.Add("Exit", null, (_, _) => _exitApp());

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "D.A.S.A — Download Automation & Security Assistant",
            Visible = true,
            ContextMenuStrip = menu,
            Icon = AppIconHelper.LoadTrayIcon()
        };

        _notifyIcon.DoubleClick += (_, _) => _showWindow();
    }

    public void ShowBalloon(string title, string message, Forms.ToolTipIcon icon = Forms.ToolTipIcon.Info)
    {
        try
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.BalloonTipIcon = icon;
            _notifyIcon.ShowBalloonTip(4000);
        }
        catch
        {
            // Balloon tips can fail in some session states; ignore.
        }
    }

    public void NotifyFileProcessed(FileProcessedPayload payload)
    {
        var title = payload.Quarantined ? "Threat quarantined" : "File organized";
        var body = payload.Quarantined
            ? Path.GetFileName(payload.FileName)
            : $"{payload.FileName} → {payload.DestinationPath}";
        ShowBalloon(title, Truncate(body, 120),
            payload.Quarantined ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}

public static class UiDispatcher
{
    public static void Invoke(Action action)
    {
        var app = Application.Current;
        if (app?.Dispatcher is null)
        {
            action();
            return;
        }

        if (app.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            app.Dispatcher.Invoke(action);
        }
    }

    public static Task InvokeAsync(Action action)
    {
        var app = Application.Current;
        if (app?.Dispatcher is null)
        {
            action();
            return Task.CompletedTask;
        }

        return app.Dispatcher.InvokeAsync(action).Task;
    }
}
