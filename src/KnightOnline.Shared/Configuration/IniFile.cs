namespace KnightOnline.Shared.Configuration;

/// <summary>
/// Small INI reader with the same semantics the servers use from shared/Ini.cpp:
/// [SECTION] headers, KEY=VALUE pairs, case-insensitive lookups, and caller
/// supplied defaults. The port deliberately keeps supporting the original
/// .ini files so an existing install's config works unchanged.
/// </summary>
public sealed class IniFile
{
    private readonly Dictionary<string, Dictionary<string, string>> _sections =
        new(StringComparer.OrdinalIgnoreCase);

    public string FilePath { get; }
    public bool FileExists { get; }

    public IniFile(string filePath)
    {
        FilePath = filePath;
        FileExists = File.Exists(filePath);
        if (FileExists)
            Load(filePath);
    }

    private void Load(string filePath)
    {
        var currentSection = string.Empty;
        _sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in File.ReadAllLines(filePath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                if (!_sections.ContainsKey(currentSection))
                    _sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator < 0)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            _sections[currentSection][key] = value;
        }
    }

    public string GetString(string section, string key, string defaultValue)
    {
        if (_sections.TryGetValue(section, out var values) &&
            values.TryGetValue(key, out var value))
            return value;

        return defaultValue;
    }

    public int GetInt(string section, string key, int defaultValue)
    {
        var text = GetString(section, key, string.Empty);
        return int.TryParse(text, out var result) ? result : defaultValue;
    }

    public bool GetBool(string section, string key, bool defaultValue)
    {
        var text = GetString(section, key, string.Empty);
        if (bool.TryParse(text, out var result))
            return result;
        return text switch
        {
            "1" => true,
            "0" => false,
            _ => defaultValue,
        };
    }
}
