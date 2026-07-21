using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DASA.Host.Services;

public interface IWindowHost
{
    bool IsMaximized { get; }
    void Minimize();
    void ToggleMaximize();
    void HideToTray();
    void BeginDragMove();
    event EventHandler? WindowStateChanged;
}

public static class WindowDragHelper
{
    private const int WmNclButtonDown = 0x00A1;
    private const int HtCaption = 0x0002;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    public static void DragMoveWindow(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;

        ReleaseCapture();
        SendMessage(handle, WmNclButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
    }
}
