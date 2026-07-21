using System.Text.Json;
using System.Text.RegularExpressions;
using DASA.Host.Models;

namespace DASA.Host.Services;

public sealed class RuleEngineService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _rulesPath;
    private readonly object _sync = new();
    private List<AutomationRule> _rules = [];

    public RuleEngineService(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _rulesPath = Path.Combine(dataDirectory, "rules.json");
        Load();
    }

    public IReadOnlyList<AutomationRule> GetRules()
    {
        lock (_sync)
        {
            return _rules
                .OrderBy(r => r.Priority)
                .ThenBy(r => r.Name)
                .Select(Clone)
                .ToList();
        }
    }

    public void SaveRule(AutomationRule rule)
    {
        lock (_sync)
        {
            var idx = _rules.FindIndex(r => r.Id == rule.Id);
            if (idx >= 0)
            {
                _rules[idx] = Clone(rule);
            }
            else
            {
                if (_rules.Count == 0)
                {
                    rule.Priority = 0;
                }
                else if (rule.Priority == 0 && _rules.All(r => r.Priority != 0))
                {
                    rule.Priority = _rules.Max(r => r.Priority) + 1;
                }

                _rules.Add(Clone(rule));
            }

            PersistUnlocked();
        }
    }

    public void DeleteRule(string id)
    {
        lock (_sync)
        {
            _rules.RemoveAll(r => r.Id == id);
            PersistUnlocked();
        }
    }

    public void Reorder(IReadOnlyList<string> orderedIds)
    {
        lock (_sync)
        {
            var map = _rules.ToDictionary(r => r.Id);
            var next = new List<AutomationRule>();
            var priority = 0;

            foreach (var id in orderedIds)
            {
                if (!map.TryGetValue(id, out var rule)) continue;
                rule.Priority = priority++;
                next.Add(rule);
                map.Remove(id);
            }

            foreach (var leftover in map.Values.OrderBy(r => r.Priority))
            {
                leftover.Priority = priority++;
                next.Add(leftover);
            }

            _rules = next;
            PersistUnlocked();
        }
    }

    public AutomationRule? Match(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath);

        lock (_sync)
        {
            foreach (var rule in _rules.Where(r => r.Enabled).OrderBy(r => r.Priority))
            {
                if (!string.IsNullOrWhiteSpace(rule.Extension))
                {
                    var expected = rule.Extension.StartsWith('.') ? rule.Extension : "." + rule.Extension;
                    if (!extension.Equals(expected, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                if (!string.IsNullOrWhiteSpace(rule.NameContains) &&
                    !MatchesPattern(fileName, rule.NameContains))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(rule.DomainContains) &&
                    fileName.IndexOf(rule.DomainContains, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(rule.Extension) &&
                    string.IsNullOrWhiteSpace(rule.NameContains) &&
                    string.IsNullOrWhiteSpace(rule.DomainContains))
                {
                    continue;
                }

                return Clone(rule);
            }
        }

        return null;
    }

    private static bool MatchesPattern(string fileName, string pattern)
    {
        if (pattern.Contains('*') || pattern.Contains('?'))
        {
            var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Regex.IsMatch(fileName, regex, RegexOptions.IgnoreCase);
        }

        return fileName.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private void Load()
    {
        if (!File.Exists(_rulesPath))
        {
            _rules = [];
            PersistUnlocked();
            return;
        }

        var json = File.ReadAllText(_rulesPath);
        _rules = JsonSerializer.Deserialize<List<AutomationRule>>(json, JsonOptions) ?? [];
    }

    private void PersistUnlocked()
    {
        var json = JsonSerializer.Serialize(_rules.OrderBy(r => r.Priority).ToList(), JsonOptions);
        File.WriteAllText(_rulesPath, json);
    }

    private static AutomationRule Clone(AutomationRule r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Enabled = r.Enabled,
        Priority = r.Priority,
        Extension = r.Extension,
        NameContains = r.NameContains,
        DomainContains = r.DomainContains,
        DestinationFolder = r.DestinationFolder,
        RenamePattern = r.RenamePattern
    };
}
