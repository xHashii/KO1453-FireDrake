using MySqlConnector;

namespace KnightOnline.Database;

public sealed partial class MariaDbCharacterRepository
{
    /// <summary>Explicit offline operator action, never callable by a network packet.
    /// Promotes only existing characters owned by the named account. Future characters
    /// are not automatically promoted. Authority 0 is AUTHORITY_GAME_MASTER.
    /// </summary>
    public async Task<int> GrantGameMasterAsync(string account)
    {
        if (string.IsNullOrWhiteSpace(account) || account.Length > 20)
            throw new ArgumentException("An existing account ID is required.", nameof(account));
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT u.strUserID FROM USERDATA u JOIN ACCOUNT_CHAR a ON " +
            "(u.strUserID = a.strCharID1 OR u.strUserID = a.strCharID2 OR u.strUserID = a.strCharID3) " +
            "JOIN TB_USER t ON t.strAccountID = a.strAccountID WHERE a.strAccountID = @account FOR UPDATE";
        command.Parameters.AddWithValue("@account", account);
        var names = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
            while (await reader.ReadAsync().ConfigureAwait(false)) names.Add(reader.GetString(0));
        foreach (var name in names)
        {
            command.CommandText = "UPDATE USERDATA SET Authority = 0 WHERE strUserID = @id";
            command.Parameters.Clear();
            command.Parameters.AddWithValue("@id", name);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        await transaction.CommitAsync().ConfigureAwait(false);
        return names.Count;
    }

    public async Task SaveVitalsAsync(string charId, int hp, int mp)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE USERDATA SET Hp = @hp, Mp = @mp WHERE strUserID = @id";
        command.Parameters.AddWithValue("@hp", hp);
        command.Parameters.AddWithValue("@mp", mp);
        command.Parameters.AddWithValue("@id", charId);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
