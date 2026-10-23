namespace KnightOnline.Database.Migrations;

/// <summary>
/// Splits a MariaDB/MySQL script into executable statements.
/// Handles: '...' / "..." / `...` quoting (with backslash and doubled-quote
/// escapes), -- and # line comments, /* */ block comments, and the client
/// side DELIMITER directive needed for stored procedures.
/// </summary>
public static class SqlScriptSplitter
{
    public static IEnumerable<string> Split(string script)
    {
        var delimiter = ";";
        var i = 0;
        var start = 0;
        var n = script.Length;

        while (i < n)
        {
            var ch = script[i];

            // DELIMITER directive is only honoured at the start of a line.
            if ((i == 0 || script[i - 1] == '\n') &&
                TryReadDelimiterDirective(script, i, out var newDelimiter, out var consumed))
            {
                if (start < i) { /* directive itself is not a statement */ }
                delimiter = newDelimiter;
                i += consumed;
                start = i;
                continue;
            }

            if (ch == '\'' || ch == '"' || ch == '`')
            {
                i = SkipQuoted(script, i, ch);
                continue;
            }

            if (ch == '-' && Peek(script, i + 1) == '-' && char.IsWhiteSpace(Peek(script, i + 2)))
            {
                i = SkipLineComment(script, i);
                continue;
            }

            if (ch == '#')
            {
                i = SkipLineComment(script, i);
                continue;
            }

            if (ch == '/' && Peek(script, i + 1) == '*')
            {
                var end = script.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? n : end + 2;
                continue;
            }

            if (MatchesDelimiter(script, i, delimiter))
            {
                var statement = script[start..i].Trim();
                if (statement.Length > 0)
                    yield return statement;
                i += delimiter.Length;
                start = i;
                continue;
            }

            i++;
        }

        var tail = script[start..].Trim();
        if (tail.Length > 0)
            yield return tail;
    }

    private static bool MatchesDelimiter(string script, int i, string delimiter)
    {
        if (i + delimiter.Length > script.Length)
            return false;
        return string.CompareOrdinal(script, i, delimiter, 0, delimiter.Length) == 0;
    }

    private static bool TryReadDelimiterDirective(string script, int i, out string delimiter, out int consumed)
    {
        delimiter = ";";
        consumed = 0;

        var j = i;
        while (j < script.Length && (script[j] == ' ' || script[j] == '\t'))
            j++;

        const string keyword = "DELIMITER";
        if (j + keyword.Length > script.Length ||
            string.Compare(script, j, keyword, 0, keyword.Length, ignoreCase: true) != 0)
            return false;

        j += keyword.Length;
        while (j < script.Length && (script[j] == ' ' || script[j] == '\t'))
            j++;

        var end = j;
        while (end < script.Length && script[end] != '\n' && script[end] != '\r')
            end++;

        var value = script[j..end].Trim();
        if (value.Length == 0)
            return false;

        delimiter = value;
        consumed = end - i;
        return true;
    }

    private static char Peek(string s, int i) => i >= 0 && i < s.Length ? s[i] : '\0';

    private static int SkipQuoted(string s, int i, char quote)
    {
        i++; // opening quote
        while (i < s.Length)
        {
            var ch = s[i];
            if (ch == '\\' && quote != '`')
            {
                i += 2;
                continue;
            }
            if (ch == quote)
            {
                if (Peek(s, i + 1) == quote) // doubled quote escape
                {
                    i += 2;
                    continue;
                }
                return i + 1;
            }
            i++;
        }
        return s.Length;
    }

    private static int SkipLineComment(string s, int i)
    {
        var nl = s.IndexOf('\n', i);
        return nl < 0 ? s.Length : nl + 1;
    }
}
