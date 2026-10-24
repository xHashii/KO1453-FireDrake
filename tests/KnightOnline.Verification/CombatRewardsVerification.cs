using System.Text.RegularExpressions;
using System.Text;
using KnightOnline.Database;
using KnightOnline.Database.Items;
using KnightOnline.Database.World;
using KnightOnline.Shared.Protocol;
using MySqlConnector;

namespace KnightOnline.Verification;

internal static class CombatRewardsVerification
{
    public static void Rules(Action<string,bool> test)
    {
        var aggro = new MonsterCombat(100,20);
        aggro.Hit(1,1000,1001);
        test("retaliation waits for attack delay and only targets attacker", !aggro.Retaliate(1002,3000,1000) && !aggro.Retaliate(1001,1500,1000) && aggro.Retaliate(1001,2000,1000) && !aggro.Retaliate(1001,2001,1000));
        aggro.Hit(999,3000,1001);
        test("dead monster cannot retaliate", !aggro.Retaliate(1001,5000,1000));
        test("respawn clears aggro", aggro.Health(23000) == 100 && !aggro.Retaliate(1001,23000,1000));
        var skill = new CombatSkill(101003,1010,3,4,0,3100,0,1,120,0,0,0);
        test("skill class/level enforced", CombatSkillRules.Learned(skill,101,3,new byte[10]) && !CombatSkillRules.Learned(skill,201,3,new byte[10]) && !CombatSkillRules.Learned(skill,101,2,new byte[10]));
        test("allocated skill tree points enforced", !CombatSkillRules.Learned(skill with { SkillClass=1055 },105,80,new byte[10]));
        var points = new byte[10]; points[0]=2;
        test("skill point allocation respects class and category", SkillPointRules.Allocate(points,5,105,10) is { } allocated && allocated[0] == 1 && allocated[5] == 1 && points[0] == 2 && SkillPointRules.Allocate(points,5,101,10) is null && SkillPointRules.Allocate(points,255,105,10) is null);
        test("master skill points require mastered class and level", SkillPointRules.Allocate(points,8,105,80) is null && SkillPointRules.Allocate(points,8,106,60) is null && SkillPointRules.Allocate(points,8,106,61) is not null);
        test("loot spawn original NPC/bundle layout", Convert.ToHexString(LootPackets.Spawn(10001,0x01020304).Payload) == "112704030201");
        test("loot box contains six padded entries", LootPackets.Open([(389014000,1)]).Length == 36 && LootPackets.Open([]).Payload.ToArray().All(b => b == 0));
        test("solo loot reply retains 1453 no-bundle-ID layout", LootPackets.Taken(900000000,7,107).Length == 12 && LootPackets.Taken(900000000,7,107).Payload[1] == 255);
        var progress = ExperienceRules.Gain(new(1,90,0,new byte[10]),250,new Dictionary<byte,long>{{1,100},{2,200},{3,400}});
        test("XP multiple levels preserves remainder and points", progress.Level == 3 && progress.Exp == 40 && progress.Points == 6);
        test("legacy empty skill blob gains usable free points", ExperienceRules.Gain(new(9,99,0,[]),1,new Dictionary<byte,long>{{9,100}}) is { Level:10, Skills.Length:10 } legacy && legacy.Skills[0] == 2);
        test("XP cap does not wrap signed storage", ExperienceRules.Gain(new(80,int.MaxValue,0,new byte[10]),int.MaxValue,new Dictionary<byte,long>()).Exp == int.MaxValue);
        var drops = new DropCatalog();
        drops.Drops[1] = new([100,0,389014000,0,0],[10000,0,10000,0,0]);
        drops.Groups[100] = Enumerable.Repeat(389020000,30).ToArray();
        var prototype = new NpcPrototype(1,"Worm",1,100,0,0,0,0,0,1,100,true,1,Money:10);
        var rolled = drops.Roll(prototype,1,(min,max) => min);
        test("drop slots resolve group/direct IDs independently plus source coin roll", rolled.Count == 3 && rolled[0] == new LootEntry(900000000,7) && rolled[1].Item == 389020000 && rolled[2].Item == 389014000);
        drops.Drops[1] = new([1,0,0,0,0],[10000,0,0,0,0]);
        drops.Grades[1] = [1000,0,0,0,0,0,0,0,0]; drops.Weapons[0] = [1000]; drops.Rarity[0] = [0,0,1000];
        test("procedural production code is not treated as ITEM ID", drops.Roll(prototype,1,(min,max) => min)[1].Item == 110110051);
    }

    public static async Task DatabaseAsync(DatabaseSettings settings, Action<string,bool> test)
    {
        var repository = new MariaDbCharacterRepository(settings);
        await repository.EnsureCombatSchemaAsync(); await repository.EnsureCombatSchemaAsync();
        await using var connection = new MySqlConnection(settings.BuildConnectionString()); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DROP TABLE IF EXISTS USERDATA,LEVEL_UP; CREATE TABLE USERDATA(strUserID varchar(21),Level tinyint unsigned,Exp int,Points smallint,strSkill varbinary(10),Gold int,strItem blob,strSerial blob,Hp smallint,Mp smallint,Zone smallint,PX int,PZ int,PY int) ENGINE=InnoDB; " +
            "CREATE TABLE LEVEL_UP(level tinyint unsigned,Exp bigint); INSERT INTO LEVEL_UP VALUES(1,100),(2,200),(3,300),(80,1000000);";
        await command.ExecuteNonQueryAsync();
        var slots = Enumerable.Repeat(ItemSlotData.Empty,75).ToArray(); slots[74] = new(65537,-8687,5044,0,0);
        var beforeBlobs = ItemBlobs.Serialize(slots);
        var before = new InventorySnapshot(beforeBlobs.Items,beforeBlobs.Serials,100);
        command.CommandText = "INSERT INTO USERDATA VALUES('combat-test',1,0,0,@skills,100,@items,@serials,100,50,21,1000,1000,0),('other-test',1,0,0,@skills,100,@items,@serials,100,50,21,1000,1000,0)";
        command.Parameters.AddWithValue("@skills",new byte[10]); command.Parameters.AddWithValue("@items",before.Items); command.Parameters.AddWithValue("@serials",before.Serials);
        await command.ExecuteNonQueryAsync(); command.Parameters.Clear();
        var npc = new WorldNpc(10001,new(150,"Worm",150,100,0,0,0,0,0,1,100,true,Experience:150),10,0,10,0);
        var token = Guid.NewGuid().ToString("N");
        var expected = new ExperienceState(1,0,0,new byte[10]);
        var reward = (await repository.AwardKillAsync("combat-test",token,npc,21,expected,[new(900000000,7),new(389014000,1)]))!;
        test("kill atomically saves XP/level/points and loot", reward.Progress.Level == 2 && reward.Progress.Exp == 50 && reward.Progress.Points == 3 && reward.Bundle > 0);
        test("duplicate kill receipt does not award XP twice", await repository.AwardKillAsync("combat-test",token,npc,21,expected,[]) is null);
        var bundle = (await repository.LoadLootAsync("combat-test",21)).Single();
        test("loot owner and zone isolation", (await repository.LoadLootAsync("other-test",21)).Count == 0 && (await repository.LoadLootAsync("combat-test",1)).Count == 0);
        var coin = new LootClaim(bundle.Id,0,900000000,7,21,10,10);
        var gold = before with { Gold=107 };
        test("wrong owner/range/zone cannot claim", await repository.TryClaimLootAsync("other-test",before,gold,coin) != InventorySaveResult.Saved && await repository.TryClaimLootAsync("combat-test",before,gold,coin with { X=100 }) != InventorySaveResult.Saved && await repository.TryClaimLootAsync("combat-test",before,gold,coin with { Zone=1 }) != InventorySaveResult.Saved);
        var claims = await Task.WhenAll(repository.TryClaimLootAsync("combat-test",before,gold,coin),repository.TryClaimLootAsync("combat-test",before,gold,coin));
        test("concurrent coin pickup commits exactly once", claims.Count(r => r == InventorySaveResult.Saved) == 1);
        slots[14] = new(389014000,1,1,123456,0); var afterBlobs = ItemBlobs.Serialize(slots);
        var after = new InventorySnapshot(afterBlobs.Items,afterBlobs.Serials,107);
        var item = new LootClaim(bundle.Id,1,389014000,1,21,10,10);
        test("stale inventory does not consume bundle entry", await repository.TryClaimLootAsync("combat-test",before,after,item) != InventorySaveResult.Saved && (await repository.LoadLootAsync("combat-test",21)).Single().Entries.Count == 1);
        command.CommandText = "ALTER TABLE USERDATA ADD CONSTRAINT inject_pickup CHECK (strUserID<>'combat-test' OR strItem=@items)";
        // CHECK cannot contain parameters on all MariaDB versions; use a fixed coin failure instead.
        command.CommandText = "ALTER TABLE USERDATA ADD CONSTRAINT inject_pickup CHECK (strUserID<>'combat-test' OR Gold=107)"; await command.ExecuteNonQueryAsync();
        var failed = false;
        try { await repository.TryClaimLootAsync("combat-test",gold,after with { Gold=108 },item); } catch (MySqlException) { failed=true; }
        test("SQL pickup failure rolls back item and claim together", failed && (await repository.LoadLootAsync("combat-test",21)).Single().Entries.Count == 1);
        command.CommandText = "ALTER TABLE USERDATA DROP CONSTRAINT inject_pickup"; await command.ExecuteNonQueryAsync();
        test("wrong drop quantity cannot consume an entry", await repository.TryClaimLootAsync("combat-test",gold,after,item with { Count=2 }) != InventorySaveResult.Saved);
        test("item pickup persists blobs and consumes entry", await repository.TryClaimLootAsync("combat-test",gold,after,item) == InventorySaveResult.Saved && (await repository.LoadLootAsync("combat-test",21)).Count == 0);
        command.CommandText = "SELECT Gold,strItem,strSerial,Level,Exp,Points FROM USERDATA WHERE strUserID='combat-test'";
        await using (var rows = await command.ExecuteReaderAsync())
        {
            await rows.ReadAsync(); test("reconnect snapshot includes rewards and preserved legacy tail", Convert.ToInt32(rows[0]) == 107 && ((byte[])rows[1]).SequenceEqual(after.Items) && ((byte[])rows[2]).SequenceEqual(after.Serials) && Convert.ToInt32(rows[3]) == 2 && Convert.ToInt32(rows[4]) == 50 && Convert.ToInt32(rows[5]) == 3);
        }
        test("retaliation vitals persisted with stale CAS protection", await repository.TrySaveCombatVitalsAsync("combat-test",100,50,80,40) && !await repository.TrySaveCombatVitalsAsync("combat-test",100,50,70,30));
        var skill = new CombatSkill(101003,1010,3,4,0,5000,0,1,120,0,0,0);
        test("skill MP and cooldown commit together", await repository.SpendCombatSkillAsync("combat-test",skill,80,40,80,40) == InventorySaveResult.Saved);
        test("skill cooldown survives new repository/reconnect", await new MariaDbCharacterRepository(settings).SpendCombatSkillAsync("combat-test",skill,80,36,80,36) == InventorySaveResult.Cooldown);
        command.CommandText = "UPDATE KO_SKILL_COOLDOWN SET AvailableAt='2000-01-01'; ALTER TABLE USERDATA ADD CONSTRAINT inject_skill CHECK (strUserID<>'combat-test' OR Mp=36)"; await command.ExecuteNonQueryAsync();
        failed=false;
        try { await repository.SpendCombatSkillAsync("combat-test",skill,80,36,80,36); } catch (MySqlException) { failed=true; }
        command.CommandText = "ALTER TABLE USERDATA DROP CONSTRAINT inject_skill"; await command.ExecuteNonQueryAsync();
        test("skill failure rolls back cooldown and can retry", failed && await repository.SpendCombatSkillAsync("combat-test",skill,80,36,80,36) == InventorySaveResult.Saved);
        var schema = await File.ReadAllTextAsync("sql/mysql/kn_online/0001_schema.sql");
        command.CommandText = Regex.Match(schema,@"CREATE TABLE `MAGIC_TYPE1`\([\s\S]*?\) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;").Value; await command.ExecuteNonQueryAsync();
        foreach (var table in new[] { "MAGIC","MAGIC_TYPE1" })
        {
            var line = File.ReadLines("sql/mysql/kn_online/0003_data.sql").First(l => l.StartsWith($"INSERT `{table}` ") && l.Contains("VALUES (101003,"));
            command.CommandText = line.Replace("INSERT `","INSERT INTO `"); await command.ExecuteNonQueryAsync();
        }
        test("actual source Slash skill definition resolves", (await repository.LoadCombatSkillAsync(101003)) is { HitPercent:120, Mp:4, SkillClass:1010 });
        test("unsupported skill never resolves as combat", await repository.LoadCombatSkillAsync(101001) is null);
        var newToken = Guid.NewGuid().ToString("N");
        var repeated = await Task.WhenAll(repository.AwardKillAsync("combat-test",newToken,npc,21,reward.Progress,[new(900000000,1)]),repository.AwardKillAsync("combat-test",newToken,npc,21,reward.Progress,[new(900000000,1)]));
        test("concurrent duplicate kill rewards commit exactly once", repeated.Count(r => r is not null) == 1);
        var expiredId = repeated.First(r => r is not null)!.Bundle;
        command.CommandText = "UPDATE KO_LOOT_BUNDLE SET ExpiresAt='2000-01-01' WHERE Id=@expired"; command.Parameters.AddWithValue("@expired",expiredId); await command.ExecuteNonQueryAsync(); command.Parameters.Clear();
        test("expired loot cannot be opened or claimed", (await repository.LoadLootAsync("combat-test",21)).Count == 0 && await repository.TryClaimLootAsync("combat-test",after,after with { Gold=108 },new(expiredId,0,900000000,1,21,10,10)) != InventorySaveResult.Saved);
        command.CommandText = "ALTER TABLE USERDATA ADD Class smallint DEFAULT 105; UPDATE USERDATA SET Level=80,strSkill=@skills WHERE strUserID='combat-test'";
        var allocation = new byte[10]; allocation[0]=2; command.Parameters.AddWithValue("@skills",allocation); await command.ExecuteNonQueryAsync(); command.Parameters.Clear();
        var allocatedSkills = SkillPointRules.Allocate(allocation,5,105,80)!;
        test("skill point allocation persists once with blob CAS", await repository.SaveAllocatedSkillAsync("combat-test",allocation,allocatedSkills,105,80) && !await repository.SaveAllocatedSkillAsync("combat-test",allocation,allocatedSkills,105,80));
        test("death and free town revival persist vitals/position", await repository.TrySaveCombatVitalsAsync("combat-test",80,32,0,32) && await repository.TrySaveCombatVitalsAsync("combat-test",0,32,100,50,new(21,"Town",30000,35000,0)));
        // Smoke-test the actual runtime loader, not just hand-built combat records.
        foreach (var table in new[] { "ZONE_INFO","K_NPC","K_NPCPOS","K_OBJECTPOS","K_MONSTER","K_MONSTER_ITEM","MAKE_ITEM_GROUP","MAKE_ITEM_GRADECODE","MAKE_WEAPON","MAKE_DEFENSIVE","MAKE_ITEM_LARECODE" })
        {
            var ddl = Regex.Match(schema,$@"CREATE TABLE `{table}`\([\s\S]*?\) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;").Value;
            command.CommandText = $"DROP TABLE IF EXISTS `{table}`; " + ddl; await command.ExecuteNonQueryAsync();
            var batch = new StringBuilder(); var count=0;
            foreach (var line in File.ReadLines("sql/mysql/kn_online/0003_data.sql").Where(l => l.StartsWith($"INSERT `{table}` ")))
            {
                batch.AppendLine(line.Replace("INSERT `","INSERT INTO `")); count++;
                if (count%128 != 0) continue;
                command.CommandText=batch.ToString(); await command.ExecuteNonQueryAsync(); batch.Clear();
            }
            if (batch.Length > 0) { command.CommandText=batch.ToString(); await command.ExecuteNonQueryAsync(); }
        }
        var data = await repository.LoadWorldDataAsync();
        var world = new WorldCatalog(data,WorldCatalog.FindMapDirectory());
        test("actual world/drop catalogs load with full shipped source", data.Monsters!.Count == 790 && world.Drops.Drops.Count == 756 && world.Drops.Groups.Count == 69 && world.Zones[21].Npcs.Count(n => n.Prototype.IsMonster) == 474);


    }
}
