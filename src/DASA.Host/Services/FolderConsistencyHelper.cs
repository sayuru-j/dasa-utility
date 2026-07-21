using System.Text.RegularExpressions;

namespace DASA.Host.Services;

/// <summary>
/// Keeps Gemini folder suggestions aligned with folders that already exist on disk.
/// </summary>
public static class FolderConsistencyHelper
{
    private static readonly Regex TrailingHashSuffix = new(@"_[a-z0-9]{6,}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TrailingCounter = new(@"\s*\(\d+\)$", RegexOptions.Compiled);

    public static List<string> ListExistingSubfolders(string sortRoot, int maxDepth = 3)
    {
        if (string.IsNullOrWhiteSpace(sortRoot) || !Directory.Exists(sortRoot))
        {
            return [];
        }

        var root = Path.GetFullPath(sortRoot);
        var results = new List<string>();
        ScanSubfolders(root, root, 0, maxDepth, results);
        return results
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Stable key for matching files that differ only by hash/counter suffixes.
    /// e.g. Gemini_Generated_Image_abc123 -> gemini_generated_image
    /// </summary>
    public static string? ExtractTrailingUniqueSuffix(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName).Trim();
        stem = TrailingCounter.Replace(stem, string.Empty);
        var beforeSuffix = TrailingHashSuffix.Replace(stem, string.Empty);
        if (string.Equals(beforeSuffix, stem, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var suffix = stem[beforeSuffix.Length..].TrimStart('_', '-', ' ');
        return string.IsNullOrWhiteSpace(suffix) ? null : suffix;
    }

    public static string ExtractSortKey(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName).Trim();
        stem = TrailingCounter.Replace(stem, string.Empty);
        stem = TrailingHashSuffix.Replace(stem, string.Empty);
        return stem.ToLowerInvariant();
    }

    public static string? MatchDestinationFromHistory(
        string fileName,
        string extension,
        string sortRoot,
        IEnumerable<(string FileName, string DestinationPath, string Source)> history)
    {
        var sortKey = ExtractSortKey(fileName);
        if (sortKey.Length < 4)
        {
            return null;
        }

        var ext = extension.ToLowerInvariant();
        foreach (var entry in history)
        {
            if (!string.Equals(entry.Source, "gemini", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(Path.GetExtension(entry.FileName), ext, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (ExtractSortKey(entry.FileName) != sortKey)
            {
                continue;
            }

            var subfolder = DestinationRelativePath(sortRoot, entry.DestinationPath);
            if (!string.IsNullOrWhiteSpace(subfolder))
            {
                return subfolder;
            }
        }

        return null;
    }

    public static string ResolveSubfolder(
        string suggested,
        IReadOnlyList<string> existingFolders,
        IReadOnlyList<RecentSortExample> recentExamples)
    {
        if (string.IsNullOrWhiteSpace(suggested))
        {
            return suggested;
        }

        foreach (var example in recentExamples)
        {
            if (string.Equals(example.DestinationSubfolder, suggested, StringComparison.OrdinalIgnoreCase))
            {
                return example.DestinationSubfolder;
            }
        }

        if (existingFolders.Count == 0)
        {
            return suggested;
        }

        foreach (var folder in existingFolders)
        {
            if (string.Equals(folder, suggested, StringComparison.OrdinalIgnoreCase))
            {
                return folder;
            }
        }

        var normalizedSuggested = NormalizePath(suggested);
        var synonymCluster = existingFolders
            .Where(folder => FolderSimilarity(normalizedSuggested, NormalizePath(folder)) >= 0.5)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (synonymCluster.Count == 0)
        {
            return suggested;
        }

        var recentFolders = recentExamples
            .Select(e => e.DestinationSubfolder)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var fromRecent = synonymCluster
            .FirstOrDefault(folder => recentFolders.Contains(folder));
        if (fromRecent is not null)
        {
            return fromRecent;
        }

        return synonymCluster
            .OrderBy(GetLeafLength)
            .ThenBy(folder => folder, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    public static string DestinationRelativePath(string sortRoot, string destinationPath)
    {
        try
        {
            var root = Path.GetFullPath(sortRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var dir = Path.GetFullPath(Path.GetDirectoryName(destinationPath) ?? destinationPath);
            if (!dir.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var relative = Path.GetRelativePath(root, dir);
            return relative is "." or ".." ? string.Empty : relative.Replace('\\', '/');
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void ScanSubfolders(string root, string current, int depth, int maxDepth, List<string> results)
    {
        if (depth >= maxDepth)
        {
            return;
        }

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(current);
        }
        catch
        {
            return;
        }

        foreach (var dir in directories)
        {
            string relative;
            try
            {
                relative = Path.GetRelativePath(root, dir).Replace('\\', '/');
            }
            catch
            {
                continue;
            }

            if (relative is "." or ".." || relative.Contains("..", StringComparison.Ordinal))
            {
                continue;
            }

            results.Add(relative);
            ScanSubfolders(root, dir, depth + 1, maxDepth, results);
        }
    }

    private static int GetLeafLength(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault()?.Length ?? path.Length;

    private static double FolderSimilarity(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0)
        {
            return 0;
        }

        if (string.Equals(a, b, StringComparison.Ordinal))
        {
            return 1;
        }

        var leafA = GetLeaf(a);
        var leafB = GetLeaf(b);
        if (string.Equals(leafA, leafB, StringComparison.Ordinal))
        {
            return 0.95;
        }

        if (a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal))
        {
            return 0.85;
        }

        if (leafA.Contains(leafB, StringComparison.Ordinal) || leafB.Contains(leafA, StringComparison.Ordinal))
        {
            return 0.8;
        }

        var tokensA = Tokenize(a);
        var tokensB = Tokenize(b);
        if (tokensA.Count == 0 || tokensB.Count == 0)
        {
            return 0;
        }

        var intersection = tokensA.Intersect(tokensB).Count();
        var union = tokensA.Union(tokensB).Count();
        var jaccard = union == 0 ? 0 : (double)intersection / union;

        var smaller = tokensA.Count <= tokensB.Count ? tokensA : tokensB;
        var larger = tokensA.Count <= tokensB.Count ? tokensB : tokensA;
        if (smaller.All(larger.Contains))
        {
            return Math.Max(jaccard, 0.72);
        }

        return jaccard;
    }

    private static string GetLeaf(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? path;

    private static HashSet<string> Tokenize(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(part => part.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(t => t.ToLowerInvariant())
            .Where(t => t.Length > 1)
            .ToHashSet(StringComparer.Ordinal);

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/')
            .Trim('/')
            .ToLowerInvariant();
}
