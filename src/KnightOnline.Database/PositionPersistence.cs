using MySqlConnector;

namespace KnightOnline.Database;

public sealed record SavedPosition(ushort Zone, int X, int Z, int Y);

public sealed partial class MariaDbCharacterRepository
{
    /// <summary>Save movement without rewriting inventory, coins, XP or vitals.
    /// CAS rejects stale pre-warp sessions and ambiguous legacy USERDATA identities.</summary>
    public async Task<bool> TrySavePositionAsync(string owner, SavedPosition expected, SavedPosition proposed)
    {
        if (proposed.Zone == 0 || proposed.X < 0 || proposed.Z < 0 || proposed.X > 655350 || proposed.Z > 655350 || proposed.Y is < -327680 or > 327670) return false;
        await using var connection = new MySqlConnection(_connectionString); await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = connection.CreateCommand(); command.Transaction=transaction;
        command.Parameters.AddWithValue("@id",owner);
        command.CommandText="SELECT strUserID FROM USERDATA WHERE strUserID=@id FOR UPDATE";
        await using (var rows = await command.ExecuteReaderAsync()) if (!await rows.ReadAsync() || await rows.ReadAsync()) return false;
        command.Parameters.AddWithValue("@zone",proposed.Zone); command.Parameters.AddWithValue("@x",proposed.X); command.Parameters.AddWithValue("@z",proposed.Z); command.Parameters.AddWithValue("@y",proposed.Y);
        command.Parameters.AddWithValue("@oldZone",expected.Zone); command.Parameters.AddWithValue("@oldX",expected.X); command.Parameters.AddWithValue("@oldZ",expected.Z); command.Parameters.AddWithValue("@oldY",expected.Y);
        command.CommandText="UPDATE USERDATA SET Zone=@zone,PX=@x,PZ=@z,PY=@y WHERE strUserID=@id AND Zone=@oldZone AND PX=@oldX AND PZ=@oldZ AND PY=@oldY";
        if (await command.ExecuteNonQueryAsync() != 1) return false;
        command.CommandText="INSERT INTO KO_WORLD_TRAVEL(strUserID) VALUES(@id) ON DUPLICATE KEY UPDATE strUserID=VALUES(strUserID)";
        await command.ExecuteNonQueryAsync(); await transaction.CommitAsync(); return true;
    }
}
