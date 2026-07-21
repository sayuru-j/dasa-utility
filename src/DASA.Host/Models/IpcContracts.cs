using System.Text.Json.Serialization;

namespace DASA.Host.Models;

public static class IpcMessageTypes
{
    // UI → Host
    public const string SaveRule = "SAVE_RULE";
    public const string DeleteRule = "DELETE_RULE";
    public const string ReorderRules = "REORDER_RULES";
    public const string UpdateSettings = "UPDATE_SETTINGS";
    public const string TriggerManualScan = "TRIGGER_MANUAL_SCAN";
    public const string UndoMove = "UNDO_MOVE";
    public const string SetMonitoring = "SET_MONITORING";
    public const string GetState = "GET_STATE";
    public const string PickFolder = "PICK_FOLDER";
    public const string WindowMinimize = "WINDOW_MINIMIZE";
    public const string WindowMaximize = "WINDOW_MAXIMIZE";
    public const string WindowClose = "WINDOW_CLOSE";
    public const string WindowDrag = "WINDOW_DRAG";
    public const string DiscoverRules = "DISCOVER_RULES";
    public const string ApplyDiscoveredRules = "APPLY_DISCOVERED_RULES";
    public const string ClearActivity = "CLEAR_ACTIVITY";
    public const string OpenInExplorer = "OPEN_IN_EXPLORER";

    // Host → UI
    public const string FileProcessed = "FILE_PROCESSED";
    public const string MalwareDetected = "MALWARE_DETECTED";
    public const string WatcherStatusChanged = "WATCHER_STATUS_CHANGED";
    public const string StateSnapshot = "STATE_SNAPSHOT";
    public const string Error = "ERROR";
    public const string SettingsUpdated = "SETTINGS_UPDATED";
    public const string RulesUpdated = "RULES_UPDATED";
    public const string WindowStateChanged = "WINDOW_STATE_CHANGED";
    public const string FolderPicked = "FOLDER_PICKED";
    public const string DiscoverRulesProgress = "DISCOVER_RULES_PROGRESS";
    public const string RulesDiscovered = "RULES_DISCOVERED";
}

public sealed class IpcEnvelope
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("requestId")]
    public string? RequestId { get; set; }

    [JsonPropertyName("payload")]
    public object? Payload { get; set; }
}

public sealed class FileProcessedPayload
{
    public string Id { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty; // amsi | rule | gemini | default
    public double? Confidence { get; set; }
    public string? UndoToken { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public bool Quarantined { get; set; }
}

public sealed class MalwareDetectedPayload
{
    public string FilePath { get; set; } = string.Empty;
    public string QuarantinePath { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WatcherStatusPayload
{
    public bool Monitoring { get; set; }
    public string WatchFolder { get; set; } = string.Empty;
}

public sealed class SettingsUpdatePayload
{
    public string? WatchFolder { get; set; }
    public string? DefaultSortRoot { get; set; }
    public string? GeminiApiKey { get; set; }
    public bool? MonitoringEnabled { get; set; }
    public bool? AmsiProtectionEnabled { get; set; }
    public bool? AutoStartWithWindows { get; set; }
    public bool? DarkMode { get; set; }
    public string? UserTaxonomy { get; set; }
    public int? WaitTimeMinutes { get; set; }
    public bool? SmartSubfoldersEnabled { get; set; }
    public bool? ShowMoveNotificationsEnabled { get; set; }
}

public sealed class SettingsViewModel
{
    public string WatchFolder { get; set; } = string.Empty;
    public string DefaultSortRoot { get; set; } = string.Empty;
    public string QuarantineFolder { get; set; } = string.Empty;
    public bool HasGeminiApiKey { get; set; }
    public bool MonitoringEnabled { get; set; }
    public bool AmsiProtectionEnabled { get; set; }
    public bool AutoStartWithWindows { get; set; }
    public bool DarkMode { get; set; } = true;
    public string UserTaxonomy { get; set; } = string.Empty;
    public int WaitTimeMinutes { get; set; }
    public bool SmartSubfoldersEnabled { get; set; }
    public bool ShowMoveNotificationsEnabled { get; set; } = true;
}

public sealed class StateSnapshotPayload
{
    public SettingsViewModel Settings { get; set; } = new();
    public List<AutomationRule> Rules { get; set; } = [];
    public List<FileProcessedPayload> History { get; set; } = [];
    public List<MalwareDetectedPayload> QuarantineEvents { get; set; } = [];
}

public sealed class UndoMovePayload
{
    public string UndoToken { get; set; } = string.Empty;
}

public sealed class ReorderRulesPayload
{
    public List<string> OrderedIds { get; set; } = [];
}

public sealed class PickFolderPayload
{
    public string Purpose { get; set; } = "watch"; // watch | sort | rule
}

public sealed class FolderPickedPayload
{
    public string Path { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
}

public sealed class WindowStatePayload
{
    public bool IsMaximized { get; set; }
}

public sealed class DiscoverRulesProgressPayload
{
    public string Phase { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class DiscoveredRuleDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Extension { get; set; }
    public string? NameContains { get; set; }
    public string DestinationFolder { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class RulesDiscoveredPayload
{
    public string Summary { get; set; } = string.Empty;
    public int FoldersScanned { get; set; }
    public List<DiscoveredRuleDto> Rules { get; set; } = [];
}

public sealed class ApplyDiscoveredRulesPayload
{
    public List<DiscoveredRuleDto> Rules { get; set; } = [];
}

public sealed class OpenInExplorerPayload
{
    public string Path { get; set; } = string.Empty;
    public bool SelectFile { get; set; } = true;
}
