using System.Diagnostics;

namespace DASA.Host.Services;

public static class ExplorerHelper
{
    public static void Open(string path, bool selectFile = true)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            if (selectFile && File.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
                return;
            }

            var directory = File.Exists(path) ? Path.GetDirectoryName(path) : path;
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;

            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true
            });
        }
        catch
        {
            // Explorer launch can fail for invalid paths; ignore.
        }
    }
}
