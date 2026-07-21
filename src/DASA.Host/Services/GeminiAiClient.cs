using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DASA.Host.Models;

namespace DASA.Host.Services;

public sealed class GeminiAiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string[] DefaultModelFallbacks =
    [
        "gemini-2.5-flash",
        "gemini-flash-latest",
        "gemini-2.5-flash-lite"
    ];

    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(90)
    };

    public async Task<GeminiCategorization?> CategorizeAsync(
        string apiKey,
        string fileName,
        string extension,
        double fileSizeMb,
        string userTaxonomy,
        IReadOnlyList<string> existingSubfolders,
        IReadOnlyList<RecentSortExample> recentExamples,
        bool smartSubfoldersEnabled,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var foldersForPrompt = smartSubfoldersEnabled
            ? SmartSubfolderHelper.TopLevelExistingFolders(existingSubfolders)
            : existingSubfolders;

        var existingJson = foldersForPrompt.Count == 0
            ? "[]"
            : JsonSerializer.Serialize(foldersForPrompt, JsonOptions);

        var recentJson = recentExamples.Count == 0
            ? "[]"
            : JsonSerializer.Serialize(recentExamples.Select(e => new
            {
                file_name = e.FileName,
                destination_subfolder = smartSubfoldersEnabled
                    ? SmartSubfolderHelper.TopLevelCategory(e.DestinationSubfolder)
                    : e.DestinationSubfolder
            }), JsonOptions);

        var smartSubfolderRules = smartSubfoldersEnabled
            ? """
            - smart_subfolders is ON: suggested_subfolder must be ONLY the top-level category (one segment, e.g. "Movies", "Documents").
            - Do NOT include title/name subfolders in suggested_subfolder — the app creates those automatically.
            - Use "Movies" for film/video files when appropriate.
            """
            : string.Empty;

        var prompt = $$"""
            You are a download file organizer. Respond with ONLY valid JSON (no markdown) matching:
            {"category":"...","suggested_subfolder":"...","clean_title":"...","confidence":0.0}
            
            Rules:
            - category must be one of the user taxonomy folders when possible.
            - suggested_subfolder is a relative path under the sort root (use / separators).
            - clean_title is a human-friendly file name without extension.
            - confidence is 0..1.
            {{smartSubfolderRules}}
            - CRITICAL: suggested_subfolder MUST be copied exactly from existing_subfolders when any entry fits. Never create new folder names that mean the same thing (e.g. never use "AI Generated" when "Generated" exists).
            - If recent_examples shows where similar files went, use that entry's top-level category only when smart_subfolders is on.
            - When no existing folder fits, prefer the shortest clear folder name and reuse it consistently.
            - Keep unique identifiers from the original file name (hashes, dates, counters) in clean_title when they distinguish duplicates.
            
            Input:
            file_name: {{fileName}}
            file_extension: {{extension}}
            file_size_mb: {{fileSizeMb:F3}}
            user_taxonomy: {{userTaxonomy}}
            smart_subfolders: {{smartSubfoldersEnabled}}
            existing_subfolders: {{existingJson}}
            recent_examples: {{recentJson}}
            """;

        return await GenerateJsonAsync<GeminiCategorization>(apiKey, prompt, temperature: 0.05, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<RuleDiscoveryResult> DiscoverRulesAsync(
        string apiKey,
        IReadOnlyList<FolderSnapshot> snapshots,
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        var folderJson = JsonSerializer.Serialize(snapshots.Select(s => new
        {
            folder = s.RelativePath,
            destination_path = s.AbsolutePath,
            file_count = s.FileCount,
            extensions = s.Extensions,
            sample_file_names = s.SampleFileNames
        }), JsonOptions);

        var prompt = $$"""
            You analyze how a Windows user organizes files and propose download automation rules.
            Respond with ONLY valid JSON (no markdown):
            {
              "summary": "one sentence overview",
              "rules": [
                {
                  "name": "Human readable rule name",
                  "extension": ".pdf",
                  "name_contains": "Invoice",
                  "destination_folder": "C:\\full\\absolute\\path",
                  "confidence": 0.0,
                  "reason": "why this rule was inferred"
                }
              ]
            }

            Guidelines:
            - Infer rules from EXISTING folder patterns (where similar files already live).
            - Each rule must include at least extension OR name_contains.
            - destination_folder MUST be an absolute path from destination_path in the scan data when possible.
            - Prefer specific rules over catch-all rules. Max 12 rules.
            - Do NOT create rules for executables/installers (.exe, .msi, .bat, .ps1, .dll).
            - confidence is 0..1 reflecting how strong the pattern is.
            - Skip generic folders like raw Downloads root unless clearly categorized.

            Context:
            watch_folder: {{settings.WatchFolder}}
            default_sort_root: {{settings.DefaultSortRoot}}
            user_taxonomy: {{settings.UserTaxonomy}}

            Scanned folders:
            {{folderJson}}
            """;

        var parsed = await GenerateJsonAsync<GeminiRuleDiscoveryResponse>(apiKey, prompt, temperature: 0.25, cancellationToken)
            .ConfigureAwait(false);

        var rules = (parsed?.Rules ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r.DestinationFolder))
            .Where(r => !string.IsNullOrWhiteSpace(r.Extension) || !string.IsNullOrWhiteSpace(r.NameContains))
            .Select(r => new DiscoveredRuleSuggestion
            {
                Name = string.IsNullOrWhiteSpace(r.Name) ? "Discovered rule" : r.Name.Trim(),
                Extension = NormalizeExtension(r.Extension),
                NameContains = string.IsNullOrWhiteSpace(r.NameContains) ? null : r.NameContains.Trim(),
                DestinationFolder = r.DestinationFolder.Trim(),
                Confidence = Math.Clamp(r.Confidence, 0, 1),
                Reason = r.Reason?.Trim() ?? string.Empty
            })
            .Take(12)
            .ToList();

        return new RuleDiscoveryResult
        {
            Summary = parsed?.Summary?.Trim() ?? $"Proposed {rules.Count} rules from your folder layout.",
            Rules = rules,
            FoldersScanned = snapshots.Count
        };
    }

    private async Task<T?> GenerateJsonAsync<T>(
        string apiKey,
        string prompt,
        double temperature,
        CancellationToken cancellationToken)
    {
        var models = ResolveModels();
        Exception? lastError = null;

        foreach (var model in models)
        {
            try
            {
                return await GenerateJsonWithModelAsync<T>(apiKey, model, prompt, temperature, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidOperationException ex) when (IsModelNotFound(ex))
            {
                lastError = ex;
            }
        }

        throw lastError ?? new InvalidOperationException("Gemini request failed.");
    }

    private static IEnumerable<string> ResolveModels()
    {
        var configured = Environment.GetEnvironmentVariable("DASA_GEMINI_MODEL");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            yield return configured.Trim();
            yield break;
        }

        foreach (var model in DefaultModelFallbacks)
        {
            yield return model;
        }
    }

    private async Task<T?> GenerateJsonWithModelAsync<T>(
        string apiKey,
        string model,
        string prompt,
        double temperature,
        CancellationToken cancellationToken)
    {
        var endpoint =
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var body = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature,
                responseMimeType = "application/json"
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Gemini API error {(int)response.StatusCode} ({model}): {ParseGeminiError(raw)}");
        }

        using var doc = JsonDocument.Parse(raw);
        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text)) return default;

        return JsonSerializer.Deserialize<T>(StripMarkdownFence(text), JsonOptions);
    }

    private static bool IsModelNotFound(InvalidOperationException ex) =>
        ex.Message.Contains("404", StringComparison.Ordinal) ||
        ex.Message.Contains("NOT_FOUND", StringComparison.OrdinalIgnoreCase);

    private static string ParseGeminiError(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? Truncate(raw, 300);
            }
        }
        catch
        {
            // fall through
        }

        return Truncate(raw, 300);
    }

    private static string? NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return null;
        var ext = extension.Trim().ToLowerInvariant();
        if (!ext.StartsWith('.')) ext = "." + ext;
        return SkipDiscoveryExtension(ext) ? null : ext;
    }

    private static bool SkipDiscoveryExtension(string ext) =>
        ext is ".exe" or ".msi" or ".bat" or ".cmd" or ".ps1" or ".vbs" or ".dll" or ".scr" or ".com";

    private sealed class GeminiRuleDiscoveryResponse
    {
        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("rules")]
        public List<GeminiRuleItem>? Rules { get; set; }
    }

    private sealed class GeminiRuleItem
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("extension")]
        public string? Extension { get; set; }

        [JsonPropertyName("name_contains")]
        public string? NameContains { get; set; }

        [JsonPropertyName("destination_folder")]
        public string DestinationFolder { get; set; } = string.Empty;

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }

    private static string StripMarkdownFence(string text)
    {
        var t = text.Trim();
        if (!t.StartsWith("```", StringComparison.Ordinal))
        {
            return t;
        }

        var firstNl = t.IndexOf('\n');
        if (firstNl < 0) return t;
        t = t[(firstNl + 1)..];
        var end = t.LastIndexOf("```", StringComparison.Ordinal);
        if (end >= 0) t = t[..end];
        return t.Trim();
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    public void Dispose() => _http.Dispose();
}

public sealed class RecentSortExample
{
    public string FileName { get; init; } = string.Empty;
    public string DestinationSubfolder { get; init; } = string.Empty;
}

public sealed class GeminiCategorization
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = "Other";

    [JsonPropertyName("suggested_subfolder")]
    public string SuggestedSubfolder { get; set; } = "Other";

    [JsonPropertyName("clean_title")]
    public string CleanTitle { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}

public static class DpapiProtector
{
    public static string Protect(string plainText)
    {
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public static string? Unprotect(string? protectedBase64)
    {
        if (string.IsNullOrWhiteSpace(protectedBase64))
        {
            return null;
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(protectedBase64);
            var bytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
