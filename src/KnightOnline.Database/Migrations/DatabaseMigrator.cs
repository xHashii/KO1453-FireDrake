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
/// SQL files ship in sql/mysql/&lt;db&gt;/ and are copied next to the server
/// executable at build time. Scripts must NOT contain CREATE DATABASE / USE:
/// the migrator already connects to the right database.
/// </summary>
public sealed class DatabaseMigrator
{
    private readonly DatabaseSettings _settings;
    private readonly string _scriptsDirectory;

    public DatabaseMigrator(DatabaseSettings settings, string scriptsDirectory)
    {
        _settings = settings;
        _scriptsDirectory = ResolveScriptsDirectory(scriptsDirectory);
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
            """, cancellationToken).ConfigureAwait(false);

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
                        "Add a new script instead of editing an applied one.");
                }
                continue;
            }

            KoLog.Info($"[db] applying {name} ...");
            foreach (var statement in SqlScriptSplitter.Split(script))
            {
                await ExecuteAsync(connection, statement, cancellationToken).ConfigureAwait(false);
            }

            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO `_ko_migrations` (script_name, checksum, applied_utc) VALUES (@name, @checksum, UTC_TIMESTAMP())";
            insert.Parameters.AddWithValue("@name", name);
            insert.Parameters.AddWithValue("@checksum", checksum);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            appliedNow++;
        }

        KoLog.Info(appliedNow == 0
            ? $"[db] {_settings.Database} is up to date ({files.Count} script(s) known)."
            : $"[db] {_settings.Database}: applied {appliedNow} migration(s).");

        return appliedNow;
    }

    private async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken)
    {
        // Connect without a default database so CREATE DATABASE can run.
        var serverSettings = _settings with { Database = string.Empty };
        await using var connection = new MySqlConnection(serverSettings.BuildConnectionString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        // Database name comes from our own config file, never from the wire.
        command.CommandText = $"CREATE DATABASE IF NOT EXISTS `{_settings.Database}`";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
        MySqlConnection connection, string sql, CancellationToken cancellationToken)
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
            // without knowing which statement actually failed.
            var snippet = sql.Length <= 200 ? sql : sql[..200] + "...";
            throw new InvalidOperationException($"Migration statement failed: {snippet}", ex);
        }
    }
}
