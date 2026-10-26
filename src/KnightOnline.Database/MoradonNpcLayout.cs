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
        await ApplyMoradonNpcLayoutAsync(1).ConfigureAwait(false);
        await ApplyMoradonNpcLayoutAsync(2).ConfigureAwait(false);
        await ApplyMoradonNpcLayoutAsync(3).ConfigureAwait(false);
    }

    private async Task ApplyMoradonNpcLayoutAsync(int layoutId)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = await ReadMoradonResource("KnightOnline.Database.MoradonLayoutSchema.sql") +
                              await ReadMoradonResource("KnightOnline.Database.MoradonRemainingSchema.sql") +
                              await ReadMoradonResource("KnightOnline.Database.MoradonAlignmentSchema.sql");
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        // A persistent sentinel row serializes concurrent startup attempts. DDL stays
        // outside the transaction; backups, changes and AppliedUtc commit together.
        command.Parameters.AddWithValue("@layout", layoutId);
        command.CommandText = "INSERT IGNORE INTO KO_MORADON_LAYOUT (LayoutId,AppliedUtc) VALUES (@layout,NULL)";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);
        command.Transaction = transaction;
        command.CommandText = "SELECT AppliedUtc FROM KO_MORADON_LAYOUT WHERE LayoutId=@layout FOR UPDATE";
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
        command.CommandText = layoutId == 3
            ? "INSERT INTO KO_MORADON_V3_NPCPOS_BACKUP SELECT * FROM K_NPCPOS WHERE ZoneID=21 AND ActType>=100"
            : layoutId == 2
            ? "INSERT INTO KO_MORADON_V2_NPCPOS_BACKUP SELECT * FROM K_NPCPOS WHERE ZoneID=21 AND ActType>=100"
            : "INSERT INTO KO_MORADON_NPCPOS_BACKUP SELECT * FROM K_NPCPOS WHERE ZoneID=21 AND ActType>=100; " +
                              "INSERT INTO KO_MORADON_OBJECTPOS_BACKUP SELECT * FROM K_OBJECTPOS WHERE ZoneID=21;";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        command.CommandText = await ReadMoradonResource(layoutId switch
        {
            1 => "KnightOnline.Database.MoradonNpcPlacements.sql",
            2 => "KnightOnline.Database.MoradonRemainingPlacements.sql",
            _ => "KnightOnline.Database.MoradonDonorAlignment.sql"
        });
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        command.CommandText = "SELECT COUNT(*) FROM K_NPCPOS p LEFT JOIN K_NPC n ON n.sSid=p.NpcID " +
                              "WHERE p.ZoneID=21 AND p.ActType>=100 AND n.sSid IS NULL";
        if (Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false)) != 0)
            throw new InvalidDataException("Moradon placement references a missing 1453 NPC definition; layout rolled back.");
        command.CommandText = "UPDATE KO_MORADON_LAYOUT SET AppliedUtc=UTC_TIMESTAMP(6) WHERE LayoutId=@layout";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        await transaction.CommitAsync().ConfigureAwait(false);
        KoLog.Info(layoutId == 1
            ? "[ MORADON NPC ] Applied matched donor layout; backed up original placements."
            : layoutId == 3
            ? "[ MORADON NPC ] Applied layout v3: remaining NPCs aligned to donor service, gate and guard anchors; exact donor placements unchanged."
            : "[ MORADON NPC ] Applied layout v2: remaining 16 legacy NPC instances moved into expanded town. Monsters unchanged; v2 backup saved.");
    }
}
