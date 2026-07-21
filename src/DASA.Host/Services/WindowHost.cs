using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

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

public static class WindowMaximizeHelper
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const uint MonitorDefaultToNearest = 0x00000002;

    public static void EnableWorkAreaMaximize(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            if (PresentationSource.FromVisual(window) is HwndSource source)
            {
                source.AddHook(WndProc);
            }
        };
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmGetMinMaxInfo)
        {
            return IntPtr.Zero;
        }

        ApplyWorkAreaMaximize(hwnd, lParam);
        handled = true;
        return IntPtr.Zero;
    }

    private static void ApplyWorkAreaMaximize(IntPtr hwnd, IntPtr lParam)
    {
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        var workArea = monitorInfo.rcWork;
        var monitorArea = monitorInfo.rcMonitor;
        var mmi = Marshal.PtrToStructure<MinMaxInfo>(lParam);

        if (HwndSource.FromHwnd(hwnd)?.RootVisual is Window window)
        {
            var dpi = VisualTreeHelper.GetDpi(window);
            mmi.ptMaxPosition.X = (int)Math.Round((workArea.Left - monitorArea.Left) / dpi.DpiScaleX);
            mmi.ptMaxPosition.Y = (int)Math.Round((workArea.Top - monitorArea.Top) / dpi.DpiScaleY);
            mmi.ptMaxSize.X = (int)Math.Round((workArea.Right - workArea.Left) / dpi.DpiScaleX);
            mmi.ptMaxSize.Y = (int)Math.Round((workArea.Bottom - workArea.Top) / dpi.DpiScaleY);
        }
        else
        {
            mmi.ptMaxPosition.X = workArea.Left - monitorArea.Left;
            mmi.ptMaxPosition.Y = workArea.Top - monitorArea.Top;
            mmi.ptMaxSize.X = workArea.Right - workArea.Left;
            mmi.ptMaxSize.Y = workArea.Bottom - workArea.Top;
        }

        Marshal.StructureToPtr(mmi, lParam, true);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point ptReserved;
        public Point ptMaxSize;
        public Point ptMaxPosition;
        public Point ptMinTrackSize;
        public Point ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpi);
}
