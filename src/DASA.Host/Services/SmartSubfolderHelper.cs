namespace DASA.Host.Services;

/// <summary>
/// Builds per-title subfolders under a category, e.g. Movies/Shrek/Shrek.mp4.
/// </summary>
public static class SmartSubfolderHelper
{
    public static string TopLevelCategory(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return "Other";
        }

        var parts = relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length == 0 ? "Other" : parts[0];
    }

    public static List<string> TopLevelExistingFolders(IReadOnlyList<string> existingFolders)
    {
        return existingFolders
            .Select(TopLevelCategory)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string GetTitleFolderName(string fileName, string? cleanTitle = null)
    {
        if (!string.IsNullOrWhiteSpace(cleanTitle))
        {
            return SanitizeFolderName(cleanTitle.Trim());
        }

        var stem = Path.GetFileNameWithoutExtension(fileName).Trim();
        stem = stem.Replace('_', ' ').Replace('.', ' ');
        stem = CollapseWhitespace(stem);
        return SanitizeFolderName(stem);
    }

    public static string Apply(string baseSubfolder, string fileName, string? cleanTitle, bool enabled)
    {
        if (!enabled || string.IsNullOrWhiteSpace(baseSubfolder))
        {
            return baseSubfolder;
        }

        var title = GetTitleFolderName(fileName, cleanTitle);
        if (string.IsNullOrWhiteSpace(title))
        {
            return baseSubfolder;
        }

        var normalized = baseSubfolder.Replace('\\', '/').Trim('/');
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return title;
        }

        if (parts.Length >= 2 &&
            string.Equals(parts[^1], title, StringComparison.OrdinalIgnoreCase))
        {
            return string.Join(Path.DirectorySeparatorChar, parts.Select(SanitizeFolderName));
        }

        var category = SanitizeFolderName(parts[0]);
        return Path.Combine(category, title);
    }

    private static string SanitizeFolderName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        name = CollapseWhitespace(name.Trim());
        return string.IsNullOrWhiteSpace(name) ? "Untitled" : name;
    }

    private static string CollapseWhitespace(string value) =>
        string.Join(' ', value.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries));
}
