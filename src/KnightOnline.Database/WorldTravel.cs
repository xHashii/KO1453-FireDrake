using MySqlConnector;

namespace KnightOnline.Database;

public sealed record TravelDestination(ushort Zone, string Name, int Px, int Pz, int Py);

/// <summary>Public prototype travel routes. Event/war zones are deliberately not exposed.</summary>
public static class TravelRules
{
    public static bool CanEnter(ushort zone, byte nation, byte level) => zone switch
    {
        21 => true,
        1 or 2 => zone == nation,
        11 or 12 => zone == nation + 10 && level >= 60,
        71 => level >= 60,
        _ => false
    };

    public static ushort? Resolve(string text, byte nation) => text.Trim().ToLowerInvariant() switch
    {
        "moradon" => 21,
        "home" or "luferson" when nation == 1 => 1,
        "home" or "elmorad" when nation == 2 => 2,
        "eslant" => (ushort)(nation + 10),
        "ronark" or "colony" or "cz" => 71,
        _ => ushort.TryParse(text, out var id) ? id : null
    };
}

public sealed partial class MariaDbCharacterRepository
{
    // Repair only the exact old default location, not every subsequent login.
    private static async Task RepairLegacySpawnAsync(MySqlConnection connection, string charId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE USERDATA u JOIN ZONE_INFO old ON old.ZoneNo = u.Zone AND old.ServerNo = 1 " +
            "JOIN ZONE_INFO m ON m.ZoneNo = 21 AND m.ServerNo = 1 " +
            "SET u.Zone = 21, u.PX = m.InitX, u.PZ = m.InitZ, u.PY = m.InitY " +
            "WHERE u.strUserID = @id AND u.Zone = u.Nation AND u.PX = old.InitX AND u.PZ = old.InitZ AND u.PY = old.InitY " +
            "AND NOT EXISTS (SELECT 1 FROM KO_WORLD_TRAVEL t WHERE t.strUserID = u.strUserID)";
        command.Parameters.AddWithValue("@id", charId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task EnsureTravelSchemaAsync()
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        // Tracks explicit travel, so returning to the old spawn never triggers the repair again.
        command.CommandText = "CREATE TABLE IF NOT EXISTS KO_WORLD_TRAVEL (strUserID varchar(21) NOT NULL PRIMARY KEY) ENGINE=InnoDB";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TravelDestination>> LoadTravelDestinationsAsync(byte nation, byte level, ushort? townZone = null)
    {
        var result = new List<TravelDestination>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT z.ZoneNo, COALESCE(z.bz,z.strZoneName), z.InitX, z.InitZ, z.InitY, " +
            "s.sKarusX, s.sKarusZ, s.sElmoradX, s.sElmoradZ FROM ZONE_INFO z " +
            "LEFT JOIN START_POSITION s ON s.ZoneID = z.ZoneNo WHERE z.ServerNo = 1 ORDER BY z.ZoneNo";
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var zone = Convert.ToUInt16(reader.GetValue(0));
            if (townZone.HasValue ? zone != townZone : !TravelRules.CanEnter(zone, nation, level))
                continue;
            var x = Convert.ToInt32(reader.GetValue(2));
            var z = Convert.ToInt32(reader.GetValue(3));
            var index = nation == 1 ? 5 : 7;
            if (!reader.IsDBNull(index) && !reader.IsDBNull(index + 1))
            {
                x = Convert.ToInt32(reader.GetValue(index)) * 100;
                z = Convert.ToInt32(reader.GetValue(index + 1)) * 100;
            }
            // Wire coordinates are unsigned decimetres.
            if (x < 0 || z < 0 || x / 10 > ushort.MaxValue || z / 10 > ushort.MaxValue)
                continue;
            result.Add(new TravelDestination(zone, reader.GetString(1), x, z, Convert.ToInt32(reader.GetValue(4))));
        }
        return result;
    }

    public async Task SaveTravelPositionAsync(string charId, TravelDestination target)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE USERDATA SET Zone=@zone, PX=@x, PZ=@z, PY=@y WHERE strUserID=@id";
        command.Parameters.AddWithValue("@id", charId);
        command.Parameters.AddWithValue("@zone", target.Zone);
        command.Parameters.AddWithValue("@x", target.Px);
        command.Parameters.AddWithValue("@z", target.Pz);
        command.Parameters.AddWithValue("@y", target.Py);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        command.CommandText = "INSERT INTO KO_WORLD_TRAVEL (strUserID) VALUES (@id) ON DUPLICATE KEY UPDATE strUserID = VALUES(strUserID)";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        await transaction.CommitAsync().ConfigureAwait(false);
    }
}
