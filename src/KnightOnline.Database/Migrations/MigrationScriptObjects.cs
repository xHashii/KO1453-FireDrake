using System.Text.RegularExpressions;

namespace KnightOnline.Database.Migrations;

/// <summary>
/// Reads a migration script well enough to know which database objects it
/// owns: the tables it creates and the tables it fills.
/// </summary>
/// <remarks>
/// <see cref="DatabaseMigrator"/> uses this to clean up after a script that
/// failed halfway (MariaDB DDL is not transactional, so the tables created
/// before the failure survive it while `_ko_migrations` still reports the
/// script as pending). The scan is deliberately forgiving - it looks at raw
/// text, so a keyword inside a string literal can produce a false positive.
/// That is harmless here: every candidate is checked against
/// INFORMATION_SCHEMA before anything is dropped or truncated.
/// </remarks>
public static class MigrationScriptObjects
{
    // (?<q>`?) ... \k<q> matches both `Table` and a bare Table, and refuses a
    // mismatched pair.
    private static readonly Regex CreateTable = new(
        @"CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<q>`?)(?<name>[A-Za-z0-9_$]+)\k<q>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Insert = new(
        @"INSERT\s+(?:IGNORE\s+)?(?:INTO\s+)?(?<q>`?)(?<name>[A-Za-z0-9_$]+)\k<q>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Distinct table names the script creates, in script order.</summary>
    public static IReadOnlyList<string> CreatedTables(string script) => Names(CreateTable, script);

    /// <summary>Distinct table names the script inserts rows into, in script order.</summary>
    public static IReadOnlyList<string> FilledTables(string script) => Names(Insert, script);

    private static IReadOnlyList<string> Names(Regex regex, string script)
    {
        var names = new List<string>();
        foreach (Match match in regex.Matches(script))
        {
            var name = match.Groups["name"].Value;
            if (name.Length > 0 && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
                names.Add(name);
        }
        return names;
    }

    /// <summary>Quotes an identifier for use in generated DDL.</summary>
    public static string Quote(string identifier) => "`" + identifier.Replace("`", "``") + "`";
}
