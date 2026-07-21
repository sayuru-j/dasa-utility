using System.Text.Json.Serialization;
using DASA.Host.Models;

namespace DASA.Host.Services;

/// <summary>
/// Scans user folder layout and infers automation rules via Gemini.
/// </summary>
public sealed class RuleDiscoveryService
{
    private const int MaxDepth = 3;
    private const int MaxFolders = 45;
    private const int MaxSampleNames = 5;

    private static readonly HashSet<string> SkipExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".bat", ".cmd", ".ps1", ".vbs", ".dll", ".scr", ".com"
    };

    private readonly SettingsService _settings;
    private readonly GeminiAiClient _gemini;

    public RuleDiscoveryService(SettingsService settings, GeminiAiClient gemini)
    {
        _settings = settings;
        _gemini = gemini;
    }

    public async Task<RuleDiscoveryResult> DiscoverAsync(
        Action<string, string>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _settings.GetGeminiApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Add a Gemini API key in Settings to use AI rule discovery.");
        }

        onProgress?.Invoke("scanning", "Scanning your folders…");
        var settings = _settings.Current;
        var snapshots = await Task.Run(() => ScanFolders(settings), cancellationToken).ConfigureAwait(false);

        if (snapshots.Count == 0)
        {
            return new RuleDiscoveryResult
            {
                Summary = "No organized subfolders with files were found to analyze.",
                Rules = []
            };
        }

        onProgress?.Invoke("analyzing", $"Analyzing {snapshots.Count} folders with Gemini…");
        return await _gemini.DiscoverRulesAsync(apiKey, snapshots, settings, cancellationToken)
            .ConfigureAwait(false);
    }

    private static List<FolderSnapshot> ScanFolders(AppSettings settings)
    {
        var roots = BuildScanRoots(settings);
        var results = new List<FolderSnapshot>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            ScanDirectory(root, root, depth: 0, results, seen);
            if (results.Count >= MaxFolders) break;
        }

        return results
            .OrderByDescending(s => s.FileCount)
            .Take(MaxFolders)
            .ToList();
    }

    private static IEnumerable<string> BuildScanRoots(AppSettings settings)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                var full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
                if (Directory.Exists(full)) unique.Add(full);
            }
            catch
            {
                // ignore invalid paths
            }
        }

        Add(settings.WatchFolder);
        Add(settings.DefaultSortRoot);
        Add(Path.Combine(profile, "Documents"));
        Add(Path.Combine(profile, "Downloads"));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));

        return unique;
    }

    private static void ScanDirectory(
        string root,
        string current,
        int depth,
        List<FolderSnapshot> results,
        HashSet<string> seen)
    {
        if (depth > MaxDepth || results.Count >= MaxFolders) return;

        try
        {
            var files = Directory.EnumerateFiles(current)
                .Where(f => !ShouldSkipFile(f))
                .Take(100)
                .ToList();

            var subdirs = Directory.EnumerateDirectories(current).Take(30).ToList();

            // Only snapshot folders that actually contain files (organized leaves or buckets).
            if (files.Count >= 2 && seen.Add(current))
            {
                var extCounts = files
                    .GroupBy(f => Path.GetExtension(f).ToLowerInvariant())
                    .Where(g => !string.IsNullOrEmpty(g.Key) && !SkipExtensions.Contains(g.Key))
                    .OrderByDescending(g => g.Count())
                    .Take(6)
                    .ToDictionary(g => g.Key, g => g.Count());

                if (extCounts.Count > 0)
                {
                    results.Add(new FolderSnapshot
                    {
                        RelativePath = ToRelativeLabel(root, current),
                        AbsolutePath = current,
                        FileCount = files.Count,
                        Extensions = extCounts,
                        SampleFileNames = files
                            .Select(f => Path.GetFileName(f))
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Take(MaxSampleNames)
                            .ToList()
                    });
                }
            }

            foreach (var sub in subdirs)
            {
                if (IsSkippedDirectory(sub)) continue;
                ScanDirectory(root, sub, depth + 1, results, seen);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // skip protected folders
        }
        catch (IOException)
        {
            // skip transient IO errors
        }
    }

    private static bool ShouldSkipFile(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name)) return true;
        if (name.StartsWith('.') || name.StartsWith('~')) return true;

        var ext = Path.GetExtension(path);
        if (SkipExtensions.Contains(ext)) return true;

        return false;
    }

    private static bool IsSkippedDirectory(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name)) return true;

        return name is "Quarantine" or "node_modules" or ".git" or "AppData"
               || name.StartsWith('.');
    }

    private static string ToRelativeLabel(string root, string current)
    {
        if (current.Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                   ?? root;
        }

        var relative = Path.GetRelativePath(root, current).Replace('\\', '/');
        var rootLabel = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(rootLabel) ? relative : $"{rootLabel}/{relative}";
    }
}

public sealed class FolderSnapshot
{
    public string RelativePath { get; set; } = string.Empty;
    public string AbsolutePath { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public Dictionary<string, int> Extensions { get; set; } = new();
    public List<string> SampleFileNames { get; set; } = [];
}

public sealed class RuleDiscoveryResult
{
    public string Summary { get; set; } = string.Empty;
    public List<DiscoveredRuleSuggestion> Rules { get; set; } = [];
    public int FoldersScanned { get; set; }
}

public sealed class DiscoveredRuleSuggestion
{
    public string Name { get; set; } = string.Empty;
    public string? Extension { get; set; }
    public string? NameContains { get; set; }
    public string DestinationFolder { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;

    [JsonIgnore]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
}
