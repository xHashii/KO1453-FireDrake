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

    /// <summary>Apply the owner-selected NPC layout stages once, backing up placements.</summary>
    public async Task EnsureMoradonNpcLayoutAsync()
    {
        await ApplyMoradonLayoutAsync(1).ConfigureAwait(false);
        await ApplyMoradonLayoutAsync(2).ConfigureAwait(false);
        await ApplyMoradonLayoutAsync(3).ConfigureAwait(false);
    }

    /// <summary>Independent one-time monster layout; never replays NPC layouts.</summary>
    public Task EnsureMoradonMonsterLayoutAsync() => ApplyMoradonLayoutAsync(4);
    public Task EnsureMoradonExactImportAsync() => ApplyMoradonLayoutAsync(5);

    private async Task ApplyMoradonLayoutAsync(int layoutId)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = await ReadMoradonResource("KnightOnline.Database.MoradonLayoutSchema.sql") +
                              await ReadMoradonResource("KnightOnline.Database.MoradonRemainingSchema.sql") +
                              await ReadMoradonResource("KnightOnline.Database.MoradonAlignmentSchema.sql") +
                              await ReadMoradonResource("KnightOnline.Database.MoradonMonsterSchema.sql") +
                              await ReadMoradonResource("KnightOnline.Database.MoradonExactSchema.sql");
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
            KoLog.Warn($"[ MORADON LAYOUT ] Skipped: zone 21 uses custom map '{map}', not moradon_expanded.smd.");
            await transaction.CommitAsync().ConfigureAwait(false);
            return;
        }
        command.CommandText = layoutId == 5
            ? "INSERT INTO KO_MORADON_PREIMPORT_NPCPOS SELECT * FROM K_NPCPOS WHERE ZoneID=21; INSERT INTO KO_MORADON_PREIMPORT_OBJECTPOS SELECT * FROM K_OBJECTPOS WHERE ZoneID=21"
            : layoutId == 4
            ? "INSERT INTO KO_MORADON_MONSTERPOS_BACKUP SELECT * FROM K_NPCPOS WHERE ZoneID=21 AND ActType<100"
            : layoutId == 3
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
            3 => "KnightOnline.Database.MoradonDonorAlignment.sql",
            4 => "KnightOnline.Database.MoradonMonsterPlacements.sql",
            _ => "KnightOnline.Database.MoradonExactImport.sql"
        });
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        command.CommandText = layoutId == 5
            ? "SELECT (SELECT COUNT(*) FROM K_NPCPOS p LEFT JOIN KO_MORADON_IMPORTED_NPC n ON n.sSid=p.NpcID WHERE p.ZoneID=21 AND p.ActType>=100 AND n.sSid IS NULL) + (SELECT COUNT(*) FROM K_NPCPOS p LEFT JOIN KO_MORADON_IMPORTED_MONSTER n ON n.sSid=p.NpcID WHERE p.ZoneID=21 AND p.ActType<100 AND n.sSid IS NULL)"
            : layoutId == 4
            ? "SELECT COUNT(*) FROM K_NPCPOS p LEFT JOIN K_MONSTER n ON n.sSid=p.NpcID WHERE p.ZoneID=21 AND p.ActType<100 AND n.sSid IS NULL"
            : "SELECT COUNT(*) FROM K_NPCPOS p LEFT JOIN K_NPC n ON n.sSid=p.NpcID WHERE p.ZoneID=21 AND p.ActType>=100 AND n.sSid IS NULL";
        if (Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false)) != 0)
            throw new InvalidDataException("Moradon placement references a missing creature definition; layout rolled back.");
        command.CommandText = "UPDATE KO_MORADON_LAYOUT SET AppliedUtc=UTC_TIMESTAMP(6) WHERE LayoutId=@layout";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        await transaction.CommitAsync().ConfigureAwait(false);
        KoLog.Info(layoutId == 5
            ? "[ MORADON IMPORT ] Applied owner upload: 48 NPCs, 375 monsters, 33 object rows. Private Moradon definitions; other zones unchanged. Previous zone rows backed up in KO_MORADON_PREIMPORT_*POS."
            : layoutId == 4
            ? "[ MORADON MONSTERS ] Applied 195 rectangles / 646 monsters: 161 exact supplied rows plus 34 adapted legacy rows. Other zones, NPCs and definitions unchanged; original zone-21 monster rows backed up in KO_MORADON_MONSTERPOS_BACKUP."
            : layoutId == 1
            ? "[ MORADON NPC ] Applied matched donor layout; backed up original placements."
            : layoutId == 3
            ? "[ MORADON NPC ] Applied layout v3: remaining NPCs aligned to donor service, gate and guard anchors; exact donor placements unchanged."
            : "[ MORADON NPC ] Applied layout v2: remaining 16 legacy NPC instances moved into expanded town. Monsters unchanged; v2 backup saved.");
    }
}
