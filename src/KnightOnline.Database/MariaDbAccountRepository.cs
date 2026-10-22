using MySqlConnector;

namespace KnightOnline.Database;

/// <summary>
/// MariaDB implementation of <see cref="IAccountRepository"/>.
/// Port of LogInServer/DBProcess.cpp (ODBC/SQL Server -> MySqlConnector/MariaDB).
/// SQL text for the two SELECTs is verbatim from the original.
/// A fresh connection per call (connections are pooled by the driver),
/// replacing the original's single shared ODBC connection.
/// </summary>
public sealed class MariaDbAccountRepository : IAccountRepository
{
    private readonly string _connectionString;

    public MariaDbAccountRepository(DatabaseSettings settings)
    {
        _connectionString = settings.BuildConnectionString();
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

        await using var command = connection.CreateCommand();
        command.CommandText = "CALL ACCOUNT_LOGIN(@account, @password, @result)";
        command.Parameters.AddWithValue("@account", accountId);
        command.Parameters.AddWithValue("@password", password);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var resultCommand = connection.CreateCommand();
        resultCommand.CommandText = "SELECT @result";
        var result = await resultCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return result is null or DBNull
            ? AccountLoginResult.Error
            : (AccountLoginResult)Convert.ToByte(result);
    }

    public async Task<short> AccountPremiumAsync(string accountId, CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "CALL ACCOUNT_PREMIUM(@account, @hours)";
        command.Parameters.AddWithValue("@account", accountId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var resultCommand = connection.CreateCommand();
        resultCommand.CommandText = "SELECT @hours";
        var result = await resultCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return result is null or DBNull ? (short)-1 : Convert.ToInt16(result);
    }
}
