namespace DASA.Host.Models;

public sealed class AutomationRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Rule";
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }

    /// <summary>Optional extension match, e.g. ".pdf" (case-insensitive).</summary>
    public string? Extension { get; set; }

    /// <summary>Substring or wildcard (*) pattern against file name.</summary>
    public string? NameContains { get; set; }

    /// <summary>Optional domain hint (matched against file name if present).</summary>
    public string? DomainContains { get; set; }

    /// <summary>Absolute or user-relative destination folder.</summary>
    public string DestinationFolder { get; set; } = string.Empty;

    /// <summary>Optional rename pattern; {name} and {ext} tokens supported.</summary>
    public string? RenamePattern { get; set; }
}
