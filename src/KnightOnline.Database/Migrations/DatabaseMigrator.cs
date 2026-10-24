using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using MySqlConnector;
using KnightOnline.Shared.Diagnostics;

namespace KnightOnline.Database.Migrations;

/// <summary>
/// "No manual imports" database bootstrapper, DbUp-style but dependency-free.
///
/// On startup it ensures the database exists, then applies every *.sql file
/// in a folder (in file-name order) that has not been applied yet. Applied
/// scripts are recorded with a SHA-256 checksum in `_ko_migrations`; if a
/// previously applied file changed on disk, that's a hard error (write a new
/// script instead of editing history).
///
/// MariaDB DDL is not transactional, so a script that fails halfway leaves the
/// tables it already created behind while `_ko_migrations` still says "not
/// applied" - and the naive retry then dies on the first CREATE TABLE with
/// "table already exists", which used to mean destroying the database by hand
/// (`docker compose down -v`). <see cref="RepairPartialApplyAsync"/> removes
/// that dead end: before a pending script runs, the leftovers of its own
/// previous attempt are cleaned up (empty tables it creates are dropped, static
/// data tables it fills are truncated). Tables that hold rows are never
/// dropped, so a repair can only ever redo work, never destroy data.
///
/// SQL files ship in sql/mysql/&lt;db&gt;/ and are copied next to the server
/// executable at build time. Scripts must NOT contain CREATE DATABASE / USE:
/// the migrator already connects to the right database.
/// </summary>
public sealed class DatabaseMigrator
{
    private readonly DatabaseSettings _settings;
    private readonly string _scriptsDirectory;
    private readonly bool _repairPartialApplies;

    /// <summary>Character set the migrated game data is written in.</summary>
    /// <remarks>
    /// The converted dump contains Turkish text (item names, notices, news).
    /// Pinning it keeps the result identical on a server whose default is
    /// latin1, where every non-ASCII character would be stored as '?'.
    /// </remarks>
    private const string DatabaseCharset = "utf8mb4";
    private const string DatabaseCollation = "utf8mb4_general_ci";

    /// <summary>Log progress on the big data scripts (140k statements) so a
    /// multi-minute import does not look like a hang.</summary>
    private const int ProgressInterval = 25_000;

    /// <summary>Scripts up to this size are counted before they run, so an
    /// error can say "statement 12/1921". Bigger ones stream instead.</summary>
    private const int CountStatementLimit = 8 * 1024 * 1024;

    /// <summary>
    /// Tables that hold live player data. A pending data script may truncate
    /// the static game-data tables it fills (that is what makes a retry
    /// idempotent), but never these: if one of them has rows, the database is
    /// in use and the operator has to decide what happens.
    /// </summary>
    private static readonly HashSet<string> PlayerTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "KO_ITEM_SEAL_SECURITY", "KO_ITEM_SEAL_LOCK", "TB_USER", "ACCOUNT_CHAR", "CURRENTUSER", "USERDATA", "USERDATA_SKILLSHORTCUT",
        "USER_SAVED_MAGIC", "USER_ITEMS", "USER_RENTAL_ITEM", "RENTAL_ITEM",
        "RENTAL_ITEM_LIST", "WAREHOUSE", "KNIGHTS", "KNIGHTS_USER", "KNIGHTS_CAPE",
        "KNIGHTS_RATING", "KNIGHTS_ALLIANCE", "KNIGHTS_SIEGE_WARFARE", "MAIL_BOX",
        "MAIL_ITEM", "FRIEND_LIST", "SEALED_ITEMS", "QUEST_LOG", "USER_DAILY_OP",
        "USER_KNIGHTS_RANK", "USER_PERSONAL_RANK", "WEB_ITEMMALL", "WEB_ITEMMALL_LOG",
        "KING_BALLOT_BOX", "KING_ELECTION_LIST", "REMOVED_USERDATA",
    };

    public DatabaseMigrator(DatabaseSettings settings, string scriptsDirectory, bool repairPartialApplies = true)
    {
        _settings = settings;
        _scriptsDirectory = ResolveScriptsDirectory(scriptsDirectory);
        _repairPartialApplies = repairPartialApplies;
    }

    // Relative script paths ("sql/kn_online") are tried against the working
    // directory first, then next to the executable (where the .csproj copies
    // the scripts at build time). This makes `dotnet run` from the repo root
    // and running the built exe from its output folder behave the same.
    private static string ResolveScriptsDirectory(string scriptsDirectory)
    {
        if (Path.IsPathRooted(scriptsDirectory) || Directory.Exists(scriptsDirectory))
            return scriptsDirectory;

        var nextToExe = Path.Combine(AppContext.BaseDirectory, scriptsDirectory);
        return Directory.Exists(nextToExe) ? nextToExe : scriptsDirectory;
    }

    /// <summary>Applies pending scripts. Returns the number applied now.</summary>
    public async Task<int> MigrateAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_scriptsDirectory))
        {
            KoLog.Warn($"No migration scripts folder at '{_scriptsDirectory}' - skipping auto-migration.");
            return 0;
        }

        var files = Directory.GetFiles(_scriptsDirectory, "*.sql")
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
            return 0;

        await EnsureDatabaseExistsAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = new MySqlConnection(_settings.BuildConnectionString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await ExecuteAsync(connection,
            """
            CREATE TABLE IF NOT EXISTS `_ko_migrations` (
              `script_name` VARCHAR(200) NOT NULL,
              `checksum`    CHAR(64)     NOT NULL,
              `applied_utc` DATETIME     NOT NULL,
              PRIMARY KEY (`script_name`)
            ) ENGINE=InnoDB
            """, cancellationToken, "bootstrap").ConfigureAwait(false);

        var applied = await LoadAppliedAsync(connection, cancellationToken).ConfigureAwait(false);
        var appliedNow = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = Path.GetFileName(file);
            var script = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script)));

            if (applied.TryGetValue(name, out var previousChecksum))
            {
                if (!string.Equals(previousChecksum, checksum, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Migration '{name}' was already applied but its content changed. " +
                        "Add a new script instead of editing an applied one. If this database is a " +
                        "throwaway dev copy, drop it (docker compose down -v) and let the servers " +
                        "rebuild it from the current scripts.");
                }
                continue;
            }

            if (_repairPartialApplies)
                await RepairPartialApplyAsync(connection, name, script, cancellationToken).ConfigureAwait(false);

            // A 67 MB data script holds ~70k statements; keeping them all in a
            // list next to the script text would double the memory of the
            // import for nothing, so only small scripts are counted up front.
            var total = script.Length <= CountStatementLimit ? CountStatements(script) : 0;

            KoLog.Info(total > 0
                ? $"[db] applying {name} ({total} statements) ..."
                : $"[db] applying {name} ...");

            var started = Stopwatch.StartNew();
            var index = 0;

            foreach (var statement in SqlScriptSplitter.Split(script))
            {
                cancellationToken.ThrowIfCancellationRequested();
                index++;

                await ExecuteAsync(connection, statement, cancellationToken,
                    total > 0 ? $"{name}, statement {index}/{total}" : $"{name}, statement {index}")
                    .ConfigureAwait(false);

                if (index % ProgressInterval == 0)
                {
                    KoLog.Info($"[db] {name}: {index}{(total > 0 ? $"/{total}" : string.Empty)} statements " +
                               $"({started.Elapsed.TotalSeconds:F0}s elapsed)");
                }
            }

            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO `_ko_migrations` (script_name, checksum, applied_utc) VALUES (@name, @checksum, UTC_TIMESTAMP())";
            insert.Parameters.AddWithValue("@name", name);
            insert.Parameters.AddWithValue("@checksum", checksum);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            appliedNow++;
            KoLog.Info($"[db] {name}: applied in {started.Elapsed.TotalSeconds:F1}s.");
        }

        KoLog.Info(appliedNow == 0
            ? $"[db] {_settings.Database} is up to date ({files.Count} script(s) known)."
            : $"[db] {_settings.Database}: applied {appliedNow} migration(s).");

        return appliedNow;
    }

    /// <summary>
    /// Cleans up after an earlier run that died partway through
    /// <paramref name="scriptName"/> (which is therefore still pending).
    /// </summary>
    /// <remarks>
    /// Nothing here can destroy data:
    /// <list type="bullet">
    /// <item>tables the script creates are dropped only when they are empty -
    /// a table with rows means the database is in use, which is reported
    /// instead of "fixed";</item>
    /// <item>tables the script inserts into are truncated only when they are
    /// static game-data tables, so a re-run cannot double 140k rows;</item>
    /// <item><see cref="PlayerTables"/> are never touched.</item>
    /// </list>
    /// </remarks>
    private async Task RepairPartialApplyAsync(
        MySqlConnection connection, string scriptName, string script, CancellationToken cancellationToken)
    {
        var creates = MigrationScriptObjects.CreatedTables(script);
        if (creates.Count > 0)
        {
            var existing = await ExistingTablesAsync(connection, creates, cancellationToken).ConfigureAwait(false);
            if (existing.Count > 0)
            {
                var withRows = await TablesWithRowsAsync(connection, existing, cancellationToken).ConfigureAwait(false);
                if (withRows.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"'{scriptName}' is not recorded as applied, yet {existing.Count} of the tables it " +
                        $"creates already exist and {withRows.Count} of them hold rows ({Summarize(withRows)}). " +
                        "Refusing to drop tables that contain data. If this is a throwaway dev database, drop it " +
                        "(docker compose down -v) and restart; otherwise apply the script by hand and record it " +
                        "in `_ko_migrations`.");
                }

                KoLog.Warn($"[db] repairing '{scriptName}': an earlier attempt stopped halfway and left " +
                           $"{existing.Count} empty table(s) behind ({Summarize(existing)}). Dropping them so the " +
                           "script applies cleanly instead of failing on 'table already exists'.");
                await DropTablesAsync(connection, existing, cancellationToken).ConfigureAwait(false);
            }
        }

        var fills = MigrationScriptObjects.FilledTables(script);
        if (fills.Count == 0)
            return;

        var present = await ExistingTablesAsync(connection, fills, cancellationToken).ConfigureAwait(false);
        if (present.Count == 0)
            return;

        var populated = await TablesWithRowsAsync(connection, present, cancellationToken).ConfigureAwait(false);
        if (populated.Count == 0)
            return;

        var playerData = populated.Where(PlayerTables.Contains).ToList();
        if (playerData.Count > 0)
        {
            throw new InvalidOperationException(
                $"'{scriptName}' is not recorded as applied, yet the player table(s) {Summarize(playerData)} it " +
                "writes to already hold rows. Refusing to truncate them. Resolve this by hand (or drop the dev " +
                "database) and then record the script in `_ko_migrations`.");
        }

        KoLog.Warn($"[db] repairing '{scriptName}': an earlier attempt stopped halfway and had already inserted " +
                   $"into {populated.Count} table(s) ({Summarize(populated)}). Truncating them so the rows are " +
                   "not duplicated on this run.");

        foreach (var table in populated)
        {
            await ExecuteAsync(connection, $"TRUNCATE TABLE {MigrationScriptObjects.Quote(table)}",
                cancellationToken, $"{scriptName}, repair").ConfigureAwait(false);
        }
    }

    /// <summary>Counts a script's statements without keeping them in memory.</summary>
    private static int CountStatements(string script)
    {
        var count = 0;
        foreach (var _ in SqlScriptSplitter.Split(script))
            count++;
        return count;
    }

    private static async Task<List<string>> ExistingTablesAsync(
        MySqlConnection connection, IReadOnlyList<string> tables, CancellationToken cancellationToken)
    {
        var found = new List<string>();
        if (tables.Count == 0)
            return found;

        await using var command = connection.CreateCommand();
        var placeholders = new string[tables.Count];
        for (var i = 0; i < tables.Count; i++)
        {
            placeholders[i] = $"@t{i}";
            command.Parameters.AddWithValue(placeholders[i], tables[i]);
        }

        command.CommandText =
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME IN (" + string.Join(", ", placeholders) + ")";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            found.Add(reader.GetString(0));

        return found;
    }

    private static async Task<List<string>> TablesWithRowsAsync(
        MySqlConnection connection, IReadOnlyList<string> tables, CancellationToken cancellationToken)
    {
        var withRows = new List<string>();
        foreach (var table in tables)
        {
            await using var command = connection.CreateCommand();
            // EXISTS stops at the first row, so this stays cheap even for the
            // 100k-row ITEM table.
            command.CommandText = $"SELECT EXISTS(SELECT 1 FROM {MigrationScriptObjects.Quote(table)})";
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (Convert.ToInt64(result) != 0)
                withRows.Add(table);
        }
        return withRows;
    }

    private static async Task DropTablesAsync(
        MySqlConnection connection, IReadOnlyList<string> tables, CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, "SET FOREIGN_KEY_CHECKS=0", cancellationToken, "repair").ConfigureAwait(false);
        try
        {
            foreach (var table in tables)
            {
                await ExecuteAsync(connection, $"DROP TABLE IF EXISTS {MigrationScriptObjects.Quote(table)}",
                    cancellationToken, "repair").ConfigureAwait(false);
            }
        }
        finally
        {
            // The script sets this itself when it needs it; leave the session
            // clean for whatever runs next.
            await ExecuteAsync(connection, "SET FOREIGN_KEY_CHECKS=1", cancellationToken, "repair").ConfigureAwait(false);
        }
    }

    private static string Summarize(IReadOnlyList<string> names)
    {
        const int shown = 6;
        if (names.Count <= shown)
            return string.Join(", ", names);

        var head = new List<string>(shown);
        for (var i = 0; i < shown; i++)
            head.Add(names[i]);
        return string.Join(", ", head) + $", ... ({names.Count} total)";
    }

    private async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken)
    {
        // Connect without a default database so CREATE DATABASE can run.
        var serverSettings = _settings with { Database = string.Empty };
        await using var connection = new MySqlConnection(serverSettings.BuildConnectionString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Database name comes from our own config file, never from the wire.
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                $"CREATE DATABASE IF NOT EXISTS {MigrationScriptObjects.Quote(_settings.Database)} " +
                $"CHARACTER SET {DatabaseCharset} COLLATE {DatabaseCollation}";
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // IF NOT EXISTS leaves an existing database alone, and its default
        // charset is what every table in 0001_schema.sql inherits.
        await using (var check = connection.CreateCommand())
        {
            check.CommandText =
                "SELECT DEFAULT_CHARACTER_SET_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = @db";
            check.Parameters.AddWithValue("@db", _settings.Database);

            var current = (await check.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) as string;
            if (current is null || string.Equals(current, DatabaseCharset, StringComparison.OrdinalIgnoreCase))
                return;

            KoLog.Warn($"[db] {_settings.Database} defaults to character set '{current}'; switching it to " +
                       $"{DatabaseCharset} so the converted Turkish game data is stored unchanged.");
            try
            {
                await using var alter = connection.CreateCommand();
                alter.CommandText =
                    $"ALTER DATABASE {MigrationScriptObjects.Quote(_settings.Database)} " +
                    $"CHARACTER SET {DatabaseCharset} COLLATE {DatabaseCollation}";
                await alter.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (MySqlException ex)
            {
                // Not fatal: the tables keep whatever charset they already had.
                KoLog.Warn($"[db] could not change the character set of {_settings.Database}: {ex.Message}");
            }
        }
    }

    private static async Task<Dictionary<string, string>> LoadAppliedAsync(
        MySqlConnection connection, CancellationToken cancellationToken)
    {
        var applied = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT script_name, checksum FROM `_ko_migrations`";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            applied[reader.GetString(0)] = reader.GetString(1);

        return applied;
    }

    private static async Task ExecuteAsync(
        MySqlConnection connection, string sql, CancellationToken cancellationToken, string? context = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 0; // huge converted data files may take a while
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (MySqlException ex)
        {
            // A bare MySqlException from one of ~140k statements is useless
            // without knowing which statement actually failed, which server
            // error it was, or what to do about it.
            var snippet = sql.Length <= 300 ? sql : sql[..300] + "...";
            var where = context is null ? string.Empty : $" ({context})";
            var hint = HintFor(ex.Number);
            throw new InvalidOperationException(
                $"Migration statement failed{where}, MySQL error {ex.Number}: " +
                snippet.ReplaceLineEndings(" ") +
                (hint is null ? string.Empty : Environment.NewLine + hint), ex);
        }
    }

    /// <summary>
    /// The server errors this project has actually hit, with the fix that
    /// applies here. Keeps a 3am startup failure self-explanatory.
    /// </summary>
    private static string? HintFor(int errorNumber) => errorNumber switch
    {
        1050 => "The table already exists, so an earlier attempt at this script did not finish. " +
                "The migrator repairs that on its own; if it declined to, the message above says why.",
        1064 => "The server rejected the syntax - look for leftover SQL Server syntax in the statement above " +
                "(tools/check_migrations.py reports those).",
        1074 => "A column is longer than this server allows (CHAR/BINARY cap at 255, VARCHAR/VARBINARY at " +
                "65535 bytes). 'python3 tools/check_migrations.py --fix' converts the offending columns " +
                "to BLOB/TEXT, which every MySQL-family server accepts.",
        1075 => "An AUTO_INCREMENT column must be part of a key.",
        1101 => "BLOB/TEXT columns cannot have a DEFAULT value.",
        1118 => "Row size too large for this server's row format. 'python3 tools/check_migrations.py --fix' " +
                "moves the widest VARCHAR columns to TEXT, which stores off-page.",
        1153 => "The statement is bigger than the server's max_allowed_packet (docker-compose.yml sets 256M).",
        1044 or 1045 => "The configured [DATABASE] user cannot create or alter this database.",
        2006 or 2013 => "The connection dropped during the migration (server restarted, packet too large, " +
                        "or a timeout).",
        _ => null,
    };
}
