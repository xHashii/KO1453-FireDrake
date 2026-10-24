using KnightOnline.Database.World;
using MySqlConnector;

namespace KnightOnline.Database;

public sealed record ExperienceState(byte Level, int Exp, short Points, byte[] Skills);
public sealed record LootBundle(uint Id, ushort Npc, ushort Zone, float X, float Z, IReadOnlyList<LootEntry> Entries, string KillToken = "");
public sealed record KillReward(ExperienceState Progress, uint Bundle);
public sealed record LootClaim(uint Bundle, byte Slot, uint Item, ushort Count, ushort Zone, float X, float Z);

public static class ExperienceRules
{
    public static ExperienceState Gain(ExperienceState old, int reward, IReadOnlyDictionary<byte,long> thresholds)
    {
        var level = old.Level; long exp = (long)old.Exp + Math.Max(0,reward); int points = old.Points;
        var skills = (byte[])old.Skills.Clone();
        while (level < 80 && thresholds.TryGetValue(level,out var threshold) && threshold > 0 && exp >= threshold)
        {
            exp -= threshold; level++;
            points = Math.Min(short.MaxValue, points + (level > 60 ? 5 : 3));
            if (level >= 10 && skills.Length > 0) skills[0] = (byte)Math.Min(255,skills[0]+2);
        }
        if (level >= 80 && thresholds.TryGetValue(level,out var cap) && cap > 0) exp = Math.Min(exp,cap);
        return new(level,(int)Math.Clamp(exp,0,int.MaxValue),(short)points,skills);
    }
}

public sealed partial class MariaDbCharacterRepository
{
    public async Task EnsureCombatSchemaAsync()
    {
        await using var connection = new MySqlConnection(_connectionString); await connection.OpenAsync();
        using var stream = typeof(MariaDbCharacterRepository).Assembly.GetManifestResourceStream("KnightOnline.Database.CombatSchema.sql")!;
        using var reader = new StreamReader(stream);
        await using var command = connection.CreateCommand(); command.CommandText = await reader.ReadToEndAsync();
        await command.ExecuteNonQueryAsync();
    }

    public async Task<KillReward?> AwardKillAsync(string owner, string token, WorldNpc npc, ushort zone,
        ExperienceState expected, IReadOnlyList<LootEntry> rolled)
    {
        if (token.Length != 32 || !npc.Prototype.IsMonster || rolled.Count > 6) throw new ArgumentException("Invalid kill reward.");
        await using var connection = new MySqlConnection(_connectionString); await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.Parameters.AddWithValue("@owner",owner); command.Parameters.AddWithValue("@token",token);
        command.CommandText = "SELECT Level,Exp,Points,strSkill FROM USERDATA WHERE strUserID=@owner FOR UPDATE";
        ExperienceState current;
        await using (var rows = await command.ExecuteReaderAsync())
        {
            if (!await rows.ReadAsync()) throw new InvalidOperationException("Missing character progression.");
            current = new(Convert.ToByte(rows[0]),Convert.ToInt32(rows[1]),Convert.ToInt16(rows[2]),(byte[])rows[3]);
            if (await rows.ReadAsync()) throw new InvalidOperationException("Ambiguous character progression.");
        }
        command.CommandText = "SELECT COUNT(*) FROM KO_KILL_REWARD WHERE KillToken=@token";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) return null;
        if (current.Level != expected.Level || current.Exp != expected.Exp || current.Points != expected.Points || !current.Skills.SequenceEqual(expected.Skills))
            throw new InvalidOperationException("Stale character progression. Reconnect required.");
        command.CommandText = "SELECT level,Exp FROM LEVEL_UP";
        var thresholds = new Dictionary<byte,long>();
        await using (var rows = await command.ExecuteReaderAsync())
            while (await rows.ReadAsync()) thresholds.Add(Convert.ToByte(rows[0]), rows.IsDBNull(1) ? 0 : Convert.ToInt64(rows[1]));
        var next = ExperienceRules.Gain(expected,npc.Prototype.Experience,thresholds);
        command.CommandText = "INSERT INTO KO_KILL_REWARD VALUES(@token,@owner,UTC_TIMESTAMP(6)); UPDATE USERDATA SET Level=@level,Exp=@exp,Points=@points,strSkill=@skills WHERE strUserID=@owner";
        command.Parameters.AddWithValue("@level",next.Level); command.Parameters.AddWithValue("@exp",next.Exp);
        command.Parameters.AddWithValue("@points",next.Points); command.Parameters.AddWithValue("@skills",next.Skills);
        await command.ExecuteNonQueryAsync();
        var entries = new List<LootEntry>();
        foreach (var entry in rolled)
        {
            if (entry.Count == 0 || entry.Item == 0) continue;
            if (entry.Item != 900000000)
            {
                command.CommandText = "SELECT COUNT(*) FROM ITEM WHERE Num=@item";
                if (command.Parameters.Contains("@item")) command.Parameters.RemoveAt("@item"); command.Parameters.AddWithValue("@item",entry.Item);
                if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1) continue;
            }
            entries.Add(entry);
        }
        uint bundle = 0;
        if (entries.Count > 0)
        {
            command.Parameters.AddWithValue("@zone",zone); command.Parameters.AddWithValue("@npc",npc.Id);
            command.Parameters.AddWithValue("@x",npc.X); command.Parameters.AddWithValue("@z",npc.Z);
            command.CommandText = "INSERT INTO KO_LOOT_BUNDLE(KillToken,Owner,Zone,NpcId,X,Z,ExpiresAt) VALUES(@token,@owner,@zone,@npc,@x,@z,TIMESTAMPADD(SECOND,120,UTC_TIMESTAMP(6)))";
            await command.ExecuteNonQueryAsync(); bundle = checked((uint)command.LastInsertedId);
            command.Parameters.AddWithValue("@bundle",bundle);
            for (var i = 0; i < entries.Count; i++)
            {
                if (command.Parameters.Contains("@item")) command.Parameters.RemoveAt("@item"); if (command.Parameters.Contains("@slot")) command.Parameters.RemoveAt("@slot"); if (command.Parameters.Contains("@count")) command.Parameters.RemoveAt("@count");
                command.Parameters.AddWithValue("@item",entries[i].Item); command.Parameters.AddWithValue("@slot",i); command.Parameters.AddWithValue("@count",entries[i].Count);
                command.CommandText = "INSERT INTO KO_LOOT_ENTRY(BundleId,Slot,ItemId,Count) VALUES(@bundle,@slot,@item,@count)";
                await command.ExecuteNonQueryAsync();
            }
        }
        await transaction.CommitAsync(); return new(next,bundle);
    }

    public async Task<IReadOnlyList<LootBundle>> LoadLootAsync(string owner, ushort zone)
    {
        await using var connection = new MySqlConnection(_connectionString); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT b.Id,b.NpcId,b.X,b.Z,e.ItemId,e.Count,e.Slot,b.KillToken FROM KO_LOOT_BUNDLE b JOIN KO_LOOT_ENTRY e ON e.BundleId=b.Id WHERE b.Owner=@owner AND b.Zone=@zone AND b.ExpiresAt>UTC_TIMESTAMP(6) AND e.Claimed=0 ORDER BY b.Id,e.Slot";
        command.Parameters.AddWithValue("@owner",owner); command.Parameters.AddWithValue("@zone",zone);
        var result = new Dictionary<uint,LootBundle>();
        await using var rows = await command.ExecuteReaderAsync();
        while (await rows.ReadAsync())
        {
            var id = Convert.ToUInt32(rows[0]);
            if (!result.TryGetValue(id,out var bundle))
            { bundle = new(id,Convert.ToUInt16(rows[1]),zone,Convert.ToSingle(rows[2]),Convert.ToSingle(rows[3]),new List<LootEntry>(),rows.GetString(7)); result.Add(id,bundle); }
            ((List<LootEntry>)bundle.Entries).Add(new(Convert.ToUInt32(rows[4]),Convert.ToUInt16(rows[5]),Convert.ToByte(rows[6])));
        }
        return result.Values.ToArray();
    }

    public Task<InventorySaveResult> TryClaimLootAsync(string owner, InventorySnapshot expected, InventorySnapshot proposed,
        LootClaim claim, uint protectedItem = 0, ulong serial = 0) => SaveInventoryAsync(owner,expected,proposed,protectedItem,serial,null,claim);

    private static async Task<bool> LockLootAsync(MySqlCommand command, LootClaim claim)
    {
        command.Parameters.AddWithValue("@bundle",claim.Bundle); command.Parameters.AddWithValue("@slot",claim.Slot);
        command.Parameters.AddWithValue("@lootItem",claim.Item); command.Parameters.AddWithValue("@count",claim.Count);
        command.Parameters.AddWithValue("@zone",claim.Zone); command.Parameters.AddWithValue("@x",claim.X); command.Parameters.AddWithValue("@z",claim.Z);
        command.CommandText = "SELECT Id FROM KO_LOOT_BUNDLE WHERE Id=@bundle AND Owner=@id AND Zone=@zone AND ExpiresAt>UTC_TIMESTAMP(6) AND POW(X-@x,2)+POW(Z-@z,2)<=100 FOR UPDATE";
        if (await command.ExecuteScalarAsync() is null) return false;
        command.CommandText = "UPDATE KO_LOOT_ENTRY SET Claimed=1 WHERE BundleId=@bundle AND Slot=@slot AND ItemId=@lootItem AND Count=@count AND Claimed=0";
        return await command.ExecuteNonQueryAsync() == 1;
    }

    public async Task<bool> TrySaveCombatVitalsAsync(string owner, int expectedHp, int expectedMp, int hp, int mp, TravelDestination? destination = null)
    {
        if (hp < 0 || mp < 0 || hp > short.MaxValue || mp > short.MaxValue) return false;
        await using var connection = new MySqlConnection(_connectionString); await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.Parameters.AddWithValue("@id",owner);
        command.CommandText = "SELECT strUserID FROM USERDATA WHERE strUserID=@id FOR UPDATE";
        await using (var reader = await command.ExecuteReaderAsync()) if (!await reader.ReadAsync() || await reader.ReadAsync()) return false;
        command.Parameters.AddWithValue("@oldHp",expectedHp); command.Parameters.AddWithValue("@oldMp",expectedMp);
        command.Parameters.AddWithValue("@hp",hp); command.Parameters.AddWithValue("@mp",mp);
        if (destination is not null)
        {
            command.Parameters.AddWithValue("@zone",destination.Zone); command.Parameters.AddWithValue("@x",destination.Px);
            command.Parameters.AddWithValue("@z",destination.Pz); command.Parameters.AddWithValue("@y",destination.Py);
        }
        command.CommandText = "UPDATE USERDATA SET Hp=@hp,Mp=@mp" + (destination is null ? "" : ",Zone=@zone,PX=@x,PZ=@z,PY=@y") + " WHERE strUserID=@id AND Hp=@oldHp AND Mp=@oldMp";
        if (await command.ExecuteNonQueryAsync() != 1) return false;
        await transaction.CommitAsync(); return true;
    }
}
