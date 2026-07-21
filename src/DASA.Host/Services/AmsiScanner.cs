using System.Runtime.InteropServices;

namespace DASA.Host.Services;

/// <summary>
/// Windows AMSI (Antivirus Scan Interface) P/Invoke wrapper for local malware checks.
/// </summary>
public sealed class AmsiScanner : IDisposable
{
    private const string AppName = "DASA";
    private IntPtr _amsiContext = IntPtr.Zero;
    private IntPtr _amsiSession = IntPtr.Zero;
    private bool _disposed;

    public bool IsAvailable { get; private set; }

    public AmsiScanner()
    {
        try
        {
            var hr = NativeMethods.AmsiInitialize(AppName, out _amsiContext);
            if (hr != 0 || _amsiContext == IntPtr.Zero)
            {
                IsAvailable = false;
                return;
            }

            hr = NativeMethods.AmsiOpenSession(_amsiContext, out _amsiSession);
            IsAvailable = hr == 0 && _amsiSession != IntPtr.Zero;
        }
        catch (DllNotFoundException)
        {
            IsAvailable = false;
        }
        catch (EntryPointNotFoundException)
        {
            IsAvailable = false;
        }
    }

    public static bool IsScannableExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".exe" or ".msi" or ".bat" or ".cmd" or ".ps1" or ".vbs" or ".js" or ".dll" or ".scr" or ".com" => true,
            _ => false
        };
    }

    /// <summary>
    /// Scans file bytes via AMSI. Returns true when malware is detected.
    /// </summary>
    public AmsiScanResult ScanFile(string filePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsAvailable)
        {
            return AmsiScanResult.Skipped("AMSI is not available on this system.");
        }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            if (bytes.Length == 0)
            {
                return AmsiScanResult.Clean("Empty file.");
            }

            // Cap very large files to first 32 MB to avoid excessive memory use.
            const int maxScan = 32 * 1024 * 1024;
            var length = Math.Min(bytes.Length, maxScan);

            var contentName = Path.GetFileName(filePath);
            var hr = NativeMethods.AmsiScanBuffer(
                _amsiContext,
                bytes,
                (uint)length,
                contentName,
                _amsiSession,
                out var result);

            if (hr != 0)
            {
                return AmsiScanResult.Error($"AmsiScanBuffer failed with HRESULT 0x{hr:X8}");
            }

            if (IsMalware(result))
            {
                return AmsiScanResult.Detected($"AMSI flagged content (result={(uint)result}).");
            }

            return AmsiScanResult.Clean($"AMSI clean (result={(uint)result}).");
        }
        catch (IOException ex)
        {
            return AmsiScanResult.Error($"Unable to read file for AMSI scan: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return AmsiScanResult.Error($"Access denied during AMSI scan: {ex.Message}");
        }
    }

    private static bool IsMalware(AmsiResult result)
    {
        // AMSI_RESULT_DETECTED = 32768; values >= DETECTED indicate malware.
        return (uint)result >= (uint)AmsiResult.Detected;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_amsiSession != IntPtr.Zero)
        {
            NativeMethods.AmsiCloseSession(_amsiContext, _amsiSession);
            _amsiSession = IntPtr.Zero;
        }

        if (_amsiContext != IntPtr.Zero)
        {
            NativeMethods.AmsiUninitialize(_amsiContext);
            _amsiContext = IntPtr.Zero;
        }
    }

    private enum AmsiResult : uint
    {
        Clean = 0,
        NotDetected = 1,
        BlockedByAdminStart = 16384,
        BlockedByAdminEnd = 20479,
        Detected = 32768
    }

    private static class NativeMethods
    {
        [DllImport("amsi.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern int AmsiInitialize(string appName, out IntPtr amsiContext);

        [DllImport("amsi.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void AmsiUninitialize(IntPtr amsiContext);

        [DllImport("amsi.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int AmsiOpenSession(IntPtr amsiContext, out IntPtr amsiSession);

        [DllImport("amsi.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void AmsiCloseSession(IntPtr amsiContext, IntPtr amsiSession);

        [DllImport("amsi.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern int AmsiScanBuffer(
            IntPtr amsiContext,
            byte[] buffer,
            uint length,
            string contentName,
            IntPtr amsiSession,
            out AmsiResult result);
    }
}

public sealed class AmsiScanResult
{
    public bool IsMalware { get; init; }
    public bool WasSkipped { get; init; }
    public bool HasError { get; init; }
    public string Detail { get; init; } = string.Empty;

    public static AmsiScanResult Clean(string detail) => new() { Detail = detail };
    public static AmsiScanResult Detected(string detail) => new() { IsMalware = true, Detail = detail };
    public static AmsiScanResult Skipped(string detail) => new() { WasSkipped = true, Detail = detail };
    public static AmsiScanResult Error(string detail) => new() { HasError = true, Detail = detail };
}
