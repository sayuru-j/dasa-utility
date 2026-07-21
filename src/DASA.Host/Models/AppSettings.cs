using System.Text.Json.Serialization;

namespace DASA.Host.Models;

public sealed class AppSettings
{
    public string WatchFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    public string QuarantineFolder { get; set; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DASA",
            "Quarantine");

    public string DefaultSortRoot { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "DASA Sorted");

    /// <summary>DPAPI-encrypted Gemini API key (Base64).</summary>
    public string? EncryptedGeminiApiKey { get; set; }

    public bool MonitoringEnabled { get; set; } = true;
    public bool AmsiProtectionEnabled { get; set; } = true;
    public bool AutoStartWithWindows { get; set; }
    public bool DarkMode { get; set; } = true;

    /// <summary>Minutes to wait after a download completes before moving it (0 = immediate).</summary>
    public int WaitTimeMinutes { get; set; }

    /// <summary>Create a title subfolder under the category, e.g. Movies/Shrek/Shrek.mp4.</summary>
    public bool SmartSubfoldersEnabled { get; set; }

    /// <summary>Show in-app move toasts and tray notifications when files are sorted.</summary>
    public bool ShowMoveNotificationsEnabled { get; set; } = true;

    /// <summary>Comma-separated folder taxonomy hints for Gemini.</summary>
    public string UserTaxonomy { get; set; } =
        "Documents, Invoices, Images, Videos, Archives, Installers, Spreadsheets, Presentations, Music, Other";

    [JsonIgnore]
    public string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DASA");
}
