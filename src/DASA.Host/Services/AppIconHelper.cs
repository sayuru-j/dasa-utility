using System.Drawing;

namespace DASA.Host.Services;

public static class AppIconHelper
{
    public static Icon LoadTrayIcon() => LoadAppIcon();

    public static Icon LoadAppIcon()
    {
        foreach (var path in ResolveCandidatePaths())
        {
            try
            {
                if (!File.Exists(path)) continue;
                return LoadIconFromPath(path);
            }
            catch
            {
                // try next candidate
            }
        }

        return SystemIcons.Shield;
    }

    private static IEnumerable<string> ResolveCandidatePaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", "icon.png");

        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            yield return Path.Combine(dir, "src", "dasa-ui", "public", "icon.ico");
            yield return Path.Combine(dir, "src", "dasa-ui", "public", "icon.png");
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
    }

    private static Icon LoadIconFromPath(string path)
    {
        if (string.Equals(Path.GetExtension(path), ".ico", StringComparison.OrdinalIgnoreCase))
        {
            using var temp = new Icon(path);
            return (Icon)temp.Clone();
        }

        using var bitmap = new Bitmap(path);
        using var fromBitmap = Icon.FromHandle(bitmap.GetHicon());
        return (Icon)fromBitmap.Clone();
    }
}
