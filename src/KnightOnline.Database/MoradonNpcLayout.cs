using KnightOnline.Shared.Diagnostics;
using MySqlConnector;

namespace KnightOnline.Database;

public sealed partial class MariaDbCharacterRepository
{
    private static async Task<string> ReadMoradonResource(string name)
    {
        using var stream = typeof(MariaDbCharacterRepository).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing embedded resource: {name}");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    /// <summary>Apply the owner-selected layout once, backing up static placements only.</summary>
    public async Task EnsureMoradonNpcLayoutAsync()
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = await ReadMoradonResource("KnightOnline.Database.MoradonLayoutSchema.sql");
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        // A persistent sentinel row serializes concurrent startup attempts. DDL stays
        // outside the transaction; backups, changes and AppliedUtc commit together.
        command.CommandText = "INSERT IGNORE INTO KO_MORADON_LAYOUT (LayoutId,AppliedUtc) VALUES (1,NULL)";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);
        command.Transaction = transaction;
        command.CommandText = "SELECT AppliedUtc FROM KO_MORADON_LAYOUT WHERE LayoutId=1 FOR UPDATE";
        var applied = await command.ExecuteScalarAsync().ConfigureAwait(false);
        if (applied is not null and not DBNull)
        {
            await transaction.CommitAsync().ConfigureAwait(false);
            return; // Never reset operator edits on subsequent starts.
        }
        command.CommandText = "SELECT strZoneName FROM ZONE_INFO WHERE ServerNo=1 AND ZoneNo=21 FOR UPDATE";
        var map = Convert.ToString(await command.ExecuteScalarAsync().ConfigureAwait(false));
        if (!string.Equals(map, "moradon_expanded.smd", StringComparison.OrdinalIgnoreCase))
        {
            KoLog.Warn($"[ MORADON NPC ] Skipped: zone 21 uses custom map '{map}', not moradon_expanded.smd.");
            await transaction.CommitAsync().ConfigureAwait(false);
            return;
        }
        command.CommandText = "INSERT INTO KO_MORADON_NPCPOS_BACKUP SELECT * FROM K_NPCPOS WHERE ZoneID=21 AND ActType>=100; " +
                              "INSERT INTO KO_MORADON_OBJECTPOS_BACKUP SELECT * FROM K_OBJECTPOS WHERE ZoneID=21;";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        command.CommandText = await ReadMoradonResource("KnightOnline.Database.MoradonNpcPlacements.sql");
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        command.CommandText = "SELECT COUNT(*) FROM K_NPCPOS p LEFT JOIN K_NPC n ON n.sSid=p.NpcID " +
                              "WHERE p.ZoneID=21 AND p.ActType>=100 AND n.sSid IS NULL";
        if (Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false)) != 0)
            throw new InvalidDataException("Moradon placement references a missing 1453 NPC definition; layout rolled back.");
        command.CommandText = "UPDATE KO_MORADON_LAYOUT SET AppliedUtc=UTC_TIMESTAMP(6) WHERE LayoutId=1";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        await transaction.CommitAsync().ConfigureAwait(false);
        KoLog.Info("[ MORADON NPC ] Applied expanded layout: 21 existing NPC IDs / 28 spawn rows, two gates and anvil. Six unmatched legacy IDs retained; monsters unchanged. Backups saved in KO_MORADON_*POS_BACKUP.");
    }
}
