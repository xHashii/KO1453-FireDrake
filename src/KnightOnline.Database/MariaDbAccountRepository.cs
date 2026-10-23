using MySqlConnector;

namespace KnightOnline.Database;

/// <summary>
/// MariaDB implementation of <see cref="IAccountRepository"/>.
/// Port of LogInServer/DBProcess.cpp (ODBC/SQL Server -> MySqlConnector/MariaDB).
/// SQL text for the two SELECTs is verbatim from the original.
/// A fresh connection per call (connections are pooled by the driver),
/// replacing the original's single shared ODBC connection.
///
/// ACCOUNT_LOGIN / ACCOUNT_PREMIUM are implemented directly here instead of
/// as MariaDB stored procedures. Their behaviour follows the real routines in
/// sql/mysql/kn_online/procedures_tsql/, with one explicit policy change:
/// the original ACCOUNT_LOGIN silently created an unknown account. In this
/// rewrite that only happens when auto-register is enabled in server config.
/// </summary>
public sealed class MariaDbAccountRepository : IAccountRepository
{
    private readonly string _connectionString;
    private readonly bool _autoRegister;

    public MariaDbAccountRepository(DatabaseSettings settings, bool autoRegister = false)
    {
        _connectionString = settings.BuildConnectionString();
        _autoRegister = autoRegister;
    }

    public async Task<IReadOnlyList<VersionInfo>> LoadVersionListAsync(CancellationToken cancellationToken = default)
    {
        var versions = new List<VersionInfo>();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT sVersion, sHistoryVersion, strFilename FROM VERSION";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            // Convert.* on purpose: the converted schema may use signed or
            // unsigned SMALLINTs depending on how the original DB was set up.
            versions.Add(new VersionInfo(
                Convert.ToUInt16(reader.GetValue(0)),
                Convert.ToUInt16(reader.GetValue(1)),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
        }

        return versions;
    }

    public async Task<IReadOnlyDictionary<int, int>> LoadUserCountsAsync(CancellationToken cancellationToken = default)
    {
        var counts = new Dictionary<int, int>();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT serverid, zone1_count, zone2_count, zone3_count FROM CONCURRENT";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var serverId = Convert.ToInt32(reader.GetValue(0));
            counts[serverId] = Convert.ToInt32(reader.GetValue(1))
                             + Convert.ToInt32(reader.GetValue(2))
                             + Convert.ToInt32(reader.GetValue(3));
        }

        return counts;
    }

    public async Task<AccountLoginResult> AccountLoginAsync(string accountId, string password, CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        string? storedPassword = null;
        var authority = 0;
        var found = false;

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT `strPasswd`, `strAuthority` FROM `TB_USER` WHERE `strAccountID` = @account";
            command.Parameters.AddWithValue("@account", accountId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            found = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (found)
            {
                storedPassword = reader.IsDBNull(0) ? null : reader.GetString(0);
                authority = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1));
            }
        }

        if (!found)
        {
            if (!_autoRegister)
                return AccountLoginResult.NotFound;

            // The original ACCOUNT_LOGIN inserted only these two columns and
            // relied on TB_USER column defaults for everything else.
            await using var insert = connection.CreateCommand();
            insert.CommandText =
                "INSERT INTO `TB_USER` (`strAccountID`, `strPasswd`) VALUES (@account, @password)";
            insert.Parameters.AddWithValue("@account", accountId);
            insert.Parameters.AddWithValue("@password", password);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            return AccountLoginResult.Success;
        }

        // SQL Server's default case-insensitive collation made the original
        // password comparison case-insensitive; keep that observable behaviour
        // for existing accounts. Deliberate deviation: a NULL stored password
        // is rejected here, whereas T-SQL's NULL comparison let it fall
        // through to success in the original procedure.
        if (storedPassword is null ||
            !string.Equals(storedPassword.TrimEnd(), password, StringComparison.OrdinalIgnoreCase))
        {
            return AccountLoginResult.InvalidPassword;
        }

        if (authority == 255)
            return AccountLoginResult.Banned;

        // ACCOUNT_LOGIN clears any stale CURRENTUSER row on a successful
        // login-server authentication.
        await using var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM `CURRENTUSER` WHERE `strAccountID` = @account";
        delete.Parameters.AddWithValue("@account", accountId);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return AccountLoginResult.Success;
    }

    public async Task<short> AccountPremiumAsync(string accountId, CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT TIMESTAMPDIFF(HOUR, NOW(), `dtPremiumTime`) FROM `TB_USER` WHERE `strAccountID` = @account";
        command.Parameters.AddWithValue("@account", accountId);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is null or DBNull)
            return -1;

        var hours = Convert.ToInt64(result);
        if (hours <= 0)
            return -1;

        return hours > short.MaxValue ? short.MaxValue : (short)hours;
    }
}
