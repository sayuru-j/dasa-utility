using DASA.Host.Models;

namespace DASA.Host.Services;

/// <summary>
/// Orchestrates the priority pipeline: AMSI → User Rules → Gemini → Default move.
/// </summary>
public sealed class FileProcessorService
{
    private readonly SettingsService _settings;
    private readonly AmsiScanner _amsi;
    private readonly RuleEngineService _rules;
    private readonly GeminiAiClient _gemini;
    private readonly HistoryStore _history;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public event EventHandler<FileProcessedPayload>? FileProcessed;
    public event EventHandler<MalwareDetectedPayload>? MalwareDetected;

    public FileProcessorService(
        SettingsService settings,
        AmsiScanner amsi,
        RuleEngineService rules,
        GeminiAiClient gemini,
        HistoryStore history)
    {
        _settings = settings;
        _amsi = amsi;
        _rules = rules;
        _gemini = gemini;
        _history = history;
    }

    public async Task ProcessAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            var settings = _settings.Current;
            var extension = Path.GetExtension(filePath);
            var fileName = Path.GetFileName(filePath);

            // 1. AMSI malware check for executables/scripts
            if (settings.AmsiProtectionEnabled && AmsiScanner.IsScannableExtension(extension))
            {
                var scan = await Task.Run(() => _amsi.ScanFile(filePath), cancellationToken).ConfigureAwait(false);
                if (scan.IsMalware)
                {
                    var quarantinePath = await QuarantineAsync(filePath, settings.QuarantineFolder).ConfigureAwait(false);
                    var malware = new MalwareDetectedPayload
                    {
                        FilePath = filePath,
                        QuarantinePath = quarantinePath,
                        Detail = scan.Detail
                    };
                    _history.AddQuarantine(malware);

                    var processed = new FileProcessedPayload
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        OriginalPath = filePath,
                        DestinationPath = quarantinePath,
                        FileName = fileName,
                        Category = "Quarantine",
                        Source = "amsi",
                        Quarantined = true
                    };
                    _history.Add(processed);

                    MalwareDetected?.Invoke(this, malware);
                    FileProcessed?.Invoke(this, processed);
                    return; // Never send executable metadata to external AI
                }
            }

            // 2. User explicit path automations
            var matchedRule = _rules.Match(filePath);
            if (matchedRule is not null)
            {
                var destFolder = ExpandPath(matchedRule.DestinationFolder);
                var destName = ApplyRename(fileName, matchedRule.RenamePattern);
                var result = await MoveWithUndoAsync(filePath, destFolder, destName, "rule", matchedRule.Name, confidence: 1.0)
                    .ConfigureAwait(false);
                FileProcessed?.Invoke(this, result);
                return;
            }

            // 3. Gemini AI categorization (skip for scannable binaries even if AMSI clean — metadata only for non-exec)
            var existingSubfolders = FolderConsistencyHelper.ListExistingSubfolders(settings.DefaultSortRoot);
            var recentExamples = BuildRecentSortExamples(settings.DefaultSortRoot, extension, fileName);
            var historyEntries = _history.GetRecent(50)
                .Select(h => (h.FileName, h.DestinationPath, h.Source))
                .ToList();

            var historySubfolder = FolderConsistencyHelper.MatchDestinationFromHistory(
                fileName,
                extension,
                settings.DefaultSortRoot,
                historyEntries);

            if (historySubfolder is not null)
            {
                var historyRelative = settings.SmartSubfoldersEnabled
                    ? SmartSubfolderHelper.Apply(
                        SmartSubfolderHelper.TopLevelCategory(historySubfolder),
                        fileName,
                        cleanTitle: null,
                        enabled: true)
                    : historySubfolder;
                var historyFolder = Path.Combine(settings.DefaultSortRoot, SanitizeRelative(historyRelative));
                var historyResult = await MoveWithUndoAsync(
                        filePath,
                        historyFolder,
                        SanitizeFileName(fileName),
                        "gemini",
                        SmartSubfolderHelper.TopLevelCategory(historyRelative),
                        confidence: 1.0)
                    .ConfigureAwait(false);
                FileProcessed?.Invoke(this, historyResult);
                return;
            }

            GeminiCategorization? ai = null;
            var apiKey = _settings.GetGeminiApiKey();
            if (!string.IsNullOrWhiteSpace(apiKey) && !AmsiScanner.IsScannableExtension(extension))
            {
                try
                {
                    var foldersForConsistency = settings.SmartSubfoldersEnabled
                        ? SmartSubfolderHelper.TopLevelExistingFolders(existingSubfolders)
                        : existingSubfolders;
                    var sizeMb = new FileInfo(filePath).Length / (1024.0 * 1024.0);
                    ai = await _gemini.CategorizeAsync(
                        apiKey,
                        fileName,
                        extension,
                        sizeMb,
                        settings.UserTaxonomy,
                        existingSubfolders,
                        recentExamples,
                        settings.SmartSubfoldersEnabled,
                        cancellationToken).ConfigureAwait(false);

                    if (ai is not null)
                    {
                        var rawSub = string.IsNullOrWhiteSpace(ai.SuggestedSubfolder) ? ai.Category : ai.SuggestedSubfolder;
                        if (settings.SmartSubfoldersEnabled)
                        {
                            rawSub = SmartSubfolderHelper.TopLevelCategory(rawSub);
                        }

                        ai.SuggestedSubfolder = FolderConsistencyHelper.ResolveSubfolder(
                            rawSub,
                            foldersForConsistency,
                            recentExamples);
                    }
                }
                catch
                {
                    ai = null;
                }
            }

            if (ai is not null)
            {
                var sub = string.IsNullOrWhiteSpace(ai.SuggestedSubfolder) ? ai.Category : ai.SuggestedSubfolder;
                if (settings.SmartSubfoldersEnabled)
                {
                    sub = SmartSubfolderHelper.Apply(sub, fileName, ai.CleanTitle, enabled: true);
                }

                var destFolder = Path.Combine(settings.DefaultSortRoot, SanitizeRelative(sub));
                var cleanName = string.IsNullOrWhiteSpace(ai.CleanTitle)
                    ? fileName
                    : PreserveUniqueSuffix(fileName, ai.CleanTitle) + extension;
                var result = await MoveWithUndoAsync(
                        filePath,
                        destFolder,
                        SanitizeFileName(cleanName),
                        "gemini",
                        SmartSubfolderHelper.TopLevelCategory(sub),
                        ai.Confidence)
                    .ConfigureAwait(false);
                FileProcessed?.Invoke(this, result);
                return;
            }

            // 4. Default action — sort by extension bucket
            var bucket = DefaultBucket(extension, settings.SmartSubfoldersEnabled);
            var defaultSub = settings.SmartSubfoldersEnabled
                ? SmartSubfolderHelper.Apply(bucket, fileName, cleanTitle: null, enabled: true)
                : bucket;
            var defaultFolder = Path.Combine(settings.DefaultSortRoot, SanitizeRelative(defaultSub));
            var defaultResult = await MoveWithUndoAsync(
                    filePath,
                    defaultFolder,
                    fileName,
                    "default",
                    bucket,
                    0.5)
                .ConfigureAwait(false);
            FileProcessed?.Invoke(this, defaultResult);
        }
        finally
        {
            _gate.Release();
        }
    }

    public bool TryUndo(string undoToken, out string? message)
    {
        message = null;
        var entry = _history.ConsumeUndoToken(undoToken);
        if (entry is null)
        {
            message = "Undo token not found or already used.";
            return false;
        }

        var (originalPath, destinationPath) = entry.Value;
        try
        {
            if (!File.Exists(destinationPath))
            {
                message = "Moved file no longer exists.";
                return false;
            }

            var restoreDir = Path.GetDirectoryName(originalPath)!;
            Directory.CreateDirectory(restoreDir);
            var restorePath = GetUniquePath(Path.Combine(restoreDir, Path.GetFileName(originalPath)));
            File.Move(destinationPath, restorePath);
            message = $"Restored to {restorePath}";
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    private async Task<FileProcessedPayload> MoveWithUndoAsync(
        string sourcePath,
        string destinationFolder,
        string destinationFileName,
        string source,
        string category,
        double? confidence)
    {
        Directory.CreateDirectory(destinationFolder);
        var destPath = GetUniquePath(Path.Combine(destinationFolder, destinationFileName));
        await MoveFileWithRetryAsync(sourcePath, destPath).ConfigureAwait(false);

        var payload = new FileProcessedPayload
        {
            Id = Guid.NewGuid().ToString("N"),
            OriginalPath = sourcePath,
            DestinationPath = destPath,
            FileName = Path.GetFileName(sourcePath),
            Category = category,
            Source = source,
            Confidence = confidence,
            UndoToken = Guid.NewGuid().ToString("N"),
            Quarantined = false
        };

        _history.Add(payload);
        return payload;
    }

    private static async Task<string> QuarantineAsync(string filePath, string quarantineFolder)
    {
        Directory.CreateDirectory(quarantineFolder);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var name = $"{stamp}_{Path.GetFileName(filePath)}";
        var dest = GetUniquePath(Path.Combine(quarantineFolder, name));
        await MoveFileWithRetryAsync(filePath, dest).ConfigureAwait(false);
        try
        {
            File.SetAttributes(dest, FileAttributes.ReadOnly);
        }
        catch
        {
            // ignore
        }

        return dest;
    }

    private static async Task MoveFileWithRetryAsync(string source, string destination)
    {
        const int maxAttempts = 10;
        for (var i = 0; i < maxAttempts; i++)
        {
            try
            {
                File.Move(source, destination, overwrite: false);
                return;
            }
            catch (IOException) when (i < maxAttempts - 1)
            {
                await Task.Delay(300 + i * 150).ConfigureAwait(false);
            }
        }

        File.Copy(source, destination, overwrite: false);
        File.Delete(source);
    }

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }

        return Path.Combine(dir, $"{name}_{Guid.NewGuid():N}{ext}");
    }

    private static string ExpandPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        path = Environment.ExpandEnvironmentVariables(path);
        if (path.StartsWith("~/") || path.StartsWith("~\\"))
        {
            path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                path[2..]);
        }

        return Path.GetFullPath(path);
    }

    private static string ApplyRename(string fileName, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return fileName;
        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        return SanitizeFileName(pattern.Replace("{name}", name, StringComparison.OrdinalIgnoreCase)
            .Replace("{ext}", ext.TrimStart('.'), StringComparison.OrdinalIgnoreCase));
    }

    private static string SanitizeRelative(string relative)
    {
        var parts = relative.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(SanitizeFileName)
            .Where(p => p is not "." and not "..")
            .ToArray();
        return parts.Length == 0 ? "Other" : Path.Combine(parts);
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "file" : name.Trim();
    }

    private List<RecentSortExample> BuildRecentSortExamples(string sortRoot, string extension, string fileName)
    {
        var ext = extension.ToLowerInvariant();
        var sortKey = FolderConsistencyHelper.ExtractSortKey(fileName);

        return _history.GetRecent(50)
            .Where(h => h.Source == "gemini" && !h.Quarantined)
            .Select(h => new
            {
                Item = h,
                Subfolder = FolderConsistencyHelper.DestinationRelativePath(sortRoot, h.DestinationPath),
                SortKey = FolderConsistencyHelper.ExtractSortKey(h.FileName)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Subfolder))
            .OrderByDescending(x =>
                string.Equals(x.SortKey, sortKey, StringComparison.Ordinal) ? 3 : 0)
            .ThenByDescending(x =>
                string.Equals(Path.GetExtension(x.Item.FileName), ext, StringComparison.OrdinalIgnoreCase) ? 2 : 0)
            .ThenByDescending(x => x.Item.Timestamp)
            .Take(8)
            .Select(x => new RecentSortExample
            {
                FileName = x.Item.FileName,
                DestinationSubfolder = x.Subfolder
            })
            .ToList();
    }

    private static string PreserveUniqueSuffix(string originalFileName, string cleanTitle)
    {
        var suffix = FolderConsistencyHelper.ExtractTrailingUniqueSuffix(originalFileName);
        return suffix is null ? cleanTitle : $"{cleanTitle} {suffix}";
    }

    private static string DefaultBucket(string extension, bool smartSubfolders) => extension.ToLowerInvariant() switch
    {
        ".pdf" or ".doc" or ".docx" or ".txt" or ".rtf" or ".odt" => "Documents",
        ".xls" or ".xlsx" or ".csv" => "Spreadsheets",
        ".ppt" or ".pptx" => "Presentations",
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" or ".svg" => "Images",
        ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" => smartSubfolders ? "Movies" : "Videos",
        ".mp3" or ".wav" or ".flac" or ".aac" or ".m4a" => "Music",
        ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "Archives",
        ".exe" or ".msi" or ".msix" or ".bat" or ".ps1" => "Installers",
        _ => "Other"
    };
}
