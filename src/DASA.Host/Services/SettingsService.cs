using System.Text.Json;
using DASA.Host.Models;
using Microsoft.Win32;

namespace DASA.Host.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _settingsPath;
    private readonly object _sync = new();
    private AppSettings _settings;

    public SettingsService()
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DASA");
        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(Path.Combine(dataDir, "Quarantine"));

        _settingsPath = Path.Combine(dataDir, "settings.json");
        _settings = LoadOrCreate(dataDir);
        EnsureFolders(_settings);
    }

    public AppSettings Current
    {
        get
        {
            lock (_sync) return Clone(_settings);
        }
    }

    public string DataDirectory => Current.DataDirectory;

    public SettingsViewModel ToViewModel()
    {
        var s = Current;
        return new SettingsViewModel
        {
            WatchFolder = s.WatchFolder,
            DefaultSortRoot = s.DefaultSortRoot,
            QuarantineFolder = s.QuarantineFolder,
            HasGeminiApiKey = !string.IsNullOrWhiteSpace(s.EncryptedGeminiApiKey),
            MonitoringEnabled = s.MonitoringEnabled,
            AmsiProtectionEnabled = s.AmsiProtectionEnabled,
            AutoStartWithWindows = s.AutoStartWithWindows,
            DarkMode = s.DarkMode,
            UserTaxonomy = s.UserTaxonomy,
            WaitTimeMinutes = s.WaitTimeMinutes,
            SmartSubfoldersEnabled = s.SmartSubfoldersEnabled
        };
    }

    public string? GetGeminiApiKey()
    {
        lock (_sync)
        {
            return DpapiProtector.Unprotect(_settings.EncryptedGeminiApiKey);
        }
    }

    public AppSettings Apply(SettingsUpdatePayload update)
    {
        lock (_sync)
        {
            if (update.WatchFolder is not null)
            {
                _settings.WatchFolder = update.WatchFolder;
            }

            if (update.DefaultSortRoot is not null)
            {
                _settings.DefaultSortRoot = update.DefaultSortRoot;
            }

            if (update.MonitoringEnabled is not null)
            {
                _settings.MonitoringEnabled = update.MonitoringEnabled.Value;
            }

            if (update.AmsiProtectionEnabled is not null)
            {
                _settings.AmsiProtectionEnabled = update.AmsiProtectionEnabled.Value;
            }

            if (update.DarkMode is not null)
            {
                _settings.DarkMode = update.DarkMode.Value;
            }

            if (update.UserTaxonomy is not null)
            {
                _settings.UserTaxonomy = update.UserTaxonomy;
            }

            if (update.GeminiApiKey is not null)
            {
                _settings.EncryptedGeminiApiKey = string.IsNullOrWhiteSpace(update.GeminiApiKey)
                    ? null
                    : DpapiProtector.Protect(update.GeminiApiKey.Trim());
            }

            if (update.AutoStartWithWindows is not null)
            {
                _settings.AutoStartWithWindows = update.AutoStartWithWindows.Value;
                SetAutoStart(_settings.AutoStartWithWindows);
            }

            if (update.WaitTimeMinutes is not null)
            {
                _settings.WaitTimeMinutes = Math.Clamp(update.WaitTimeMinutes.Value, 0, 1440);
            }

            if (update.SmartSubfoldersEnabled is not null)
            {
                _settings.SmartSubfoldersEnabled = update.SmartSubfoldersEnabled.Value;
            }

            EnsureFolders(_settings);
            PersistUnlocked();
            return Clone(_settings);
        }
    }

    private AppSettings LoadOrCreate(string dataDir)
    {
        if (!File.Exists(_settingsPath))
        {
            var defaults = new AppSettings();
            Persist(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            var before = settings.DefaultSortRoot;
            MigrateLegacyDefaults(settings);
            if (!string.Equals(before, settings.DefaultSortRoot, StringComparison.Ordinal))
            {
                Persist(settings);
            }

            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    private static void MigrateLegacyDefaults(AppSettings settings)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var legacySortRoot = Path.Combine(profile, "Documents", "DASA Sorted");
        var downloadsSortRoot = Path.Combine(profile, "Downloads", "DASA Sorted");

        if (string.Equals(settings.DefaultSortRoot, legacySortRoot, StringComparison.OrdinalIgnoreCase))
        {
            settings.DefaultSortRoot = downloadsSortRoot;
        }
    }

    private void PersistUnlocked() => Persist(_settings);

    private void Persist(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    private static void EnsureFolders(AppSettings settings)
    {
        Directory.CreateDirectory(settings.DataDirectory);
        Directory.CreateDirectory(settings.QuarantineFolder);
        Directory.CreateDirectory(settings.DefaultSortRoot);
        if (!string.IsNullOrWhiteSpace(settings.WatchFolder))
        {
            Directory.CreateDirectory(settings.WatchFolder);
        }
    }

    private static void SetAutoStart(bool enabled)
    {
        const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        using var key = Registry.CurrentUser.OpenSubKey(runKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(runKey);

        const string valueName = "DASA";
        if (enabled)
        {
            var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "DASA.exe");
            key.SetValue(valueName, $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }

    private static AppSettings Clone(AppSettings s) => new()
    {
        WatchFolder = s.WatchFolder,
        QuarantineFolder = s.QuarantineFolder,
        DefaultSortRoot = s.DefaultSortRoot,
        EncryptedGeminiApiKey = s.EncryptedGeminiApiKey,
        MonitoringEnabled = s.MonitoringEnabled,
        AmsiProtectionEnabled = s.AmsiProtectionEnabled,
        AutoStartWithWindows = s.AutoStartWithWindows,
        DarkMode = s.DarkMode,
        UserTaxonomy = s.UserTaxonomy,
        WaitTimeMinutes = s.WaitTimeMinutes,
        SmartSubfoldersEnabled = s.SmartSubfoldersEnabled
    };
}
