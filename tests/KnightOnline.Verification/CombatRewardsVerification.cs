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
        var sprint=new SelfSpeedSkill(new(206001,2060,1,5,0,6000,0,4,0,0,0,0),10,150);
        var capturedMagic=Packet.FromReceived(WizOpcode.MagicProcess,Convert.FromHexString("03B124030001000100000000000000000000000000"));
        test("captured self-target skill parses as Sprint, not a target-based potion inference",MagicPackets.TryRead(capturedMagic,out var capturedSkill)&&capturedSkill.Skill==206001&&capturedSkill.Caster==1&&capturedSkill.Target==1);
        test("Sprint native type4 duration and speed fields",Convert.ToHexString(SelfSpeedPackets.Applied(sprint,1).Payload)=="03B1240300010001000000010000000A00000096000000");
        test("speed removal uses native category six",Convert.ToHexString(SelfSpeedPackets.Removed().Payload)=="0506");
        var effect=new SpeedEffect();effect.Apply(sprint,1000);
        test("speed effect expires at authored duration and resets movement percent",effect.Active(10999)&&effect.Percent==150&&effect.Expire(11000)&&effect.Percent==100&&!effect.Expire(11001));
        effect.Apply(sprint,12000);test("speed death/travel/disconnect clear is idempotent",effect.Clear()&&!effect.Clear()&&!effect.Active(12000)&&effect.Percent==100);
        test("Sprint remains class and level restricted",CombatSkillRules.Learned(sprint.Skill,206,1,[])&&!CombatSkillRules.Learned(sprint.Skill,106,80,[])&&!CombatSkillRules.Learned(sprint.Skill,206,0,[]));
        var heal=new SelfHealingSkill(new(112001,1120,1,5,1900,500,0,3,0,0,0,0),15);
        test("self healing caps HP and refuses full/dead players",HealingRules.SelfHp(heal,90,100)==100&&HealingRules.SelfHp(heal,50,100)==65&&HealingRules.SelfHp(heal,100,100) is null&&HealingRules.SelfHp(heal,0,100) is null);
        test("priest healing enforces class and learned level",CombatSkillRules.Learned(heal.Skill,112,1,[])&&!CombatSkillRules.Learned(heal.Skill,206,80,[]));
        var aggro = new MonsterCombat(100,20);
        aggro.Hit(1,1000,1001);
        test("retaliation waits for attack delay and only targets attacker", !aggro.Retaliate(1002,3000,1000) && !aggro.Retaliate(1001,1500,1000) && aggro.Retaliate(1001,2000,1000) && !aggro.Retaliate(1001,2001,1000));
        var continuous = new MonsterCombat(100,20);
        continuous.Hit(1,1000,1001); continuous.Hit(1,2100,1001);
        test("continuous player hits cannot postpone retaliation", continuous.Retaliate(1001,2200,1500));
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
        command.CommandText = "DROP TABLE IF EXISTS KO_MORADON_V2_NPCPOS_BACKUP,KO_MORADON_LAYOUT,KO_MORADON_NPCPOS_BACKUP,KO_MORADON_OBJECTPOS_BACKUP,USERDATA,LEVEL_UP; CREATE TABLE USERDATA(strUserID varchar(21),Level tinyint unsigned,Exp int,Points smallint,strSkill varbinary(10),Gold int,strItem blob,strSerial blob,Hp smallint,Mp smallint,Zone smallint,PX int,PZ int,PY int) ENGINE=InnoDB; " +
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
        command.CommandText=Regex.Match(schema,@"CREATE TABLE `MAGIC_TYPE4`\([\s\S]*?\) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;").Value;await command.ExecuteNonQueryAsync();
        foreach(var table in new[]{"MAGIC","MAGIC_TYPE4"})
        {
            command.CommandText=File.ReadLines("sql/mysql/kn_online/0003_data.sql").First(l=>l.StartsWith($"INSERT `{table}` ")&&l.Contains("VALUES (206001,")).Replace("INSERT `","INSERT INTO `");await command.ExecuteNonQueryAsync();
        }
        var sprint=(await repository.LoadSelfSpeedSkillAsync(206001))!;
        test("captured Sprint resolves from actual MAGIC/TYPE4, never potion",sprint is {Duration:10,Speed:150,Skill.Mp:5}&&await repository.LoadPotionAsync(206001) is null);
        test("attack and potion do not resolve as self-speed",await repository.LoadSelfSpeedSkillAsync(101003) is null&&await repository.LoadSelfSpeedSkillAsync(490014) is null);
        command.CommandText="INSERT INTO USERDATA(strUserID,Hp,Mp,Gold,strItem,strSerial) VALUES('speed-test',100,50,123,X'010203',X'040506')";await command.ExecuteNonQueryAsync();
        test("Sprint spends MP transactionally",await repository.SpendCombatSkillAsync("speed-test",sprint.Skill,100,50,100,50)==InventorySaveResult.Saved);
        test("Sprint cooldown persists across reconnect",await new MariaDbCharacterRepository(settings).SpendCombatSkillAsync("speed-test",sprint.Skill,100,45,100,45)==InventorySaveResult.Cooldown);
        command.CommandText="SELECT Mp,Gold,HEX(strItem),HEX(strSerial) FROM USERDATA WHERE strUserID='speed-test'";
        await using(var r=await command.ExecuteReaderAsync()){await r.ReadAsync();test("Sprint cannot consume a potion or alter inventory/coins",Convert.ToInt32(r[0])==45&&Convert.ToInt32(r[1])==123&&Convert.ToString(r[2])=="010203"&&Convert.ToString(r[3])=="040506");}
        command.CommandText="UPDATE MAGIC_TYPE4 SET AC=1 WHERE iNum=206001";await command.ExecuteNonQueryAsync();
        test("unsupported mixed buff never silently applies speed alone",await repository.LoadSelfSpeedSkillAsync(206001) is null);
        command.CommandText="UPDATE MAGIC_TYPE4 SET AC=0 WHERE iNum=206001";await command.ExecuteNonQueryAsync();

        foreach(var table in new[]{"MAGIC","MAGIC_TYPE3"})
        {
            command.CommandText=File.ReadLines("sql/mysql/kn_online/0003_data.sql").First(l=>l.StartsWith($"INSERT `{table}` ")&&l.Contains("VALUES (112001,")).Replace("INSERT `","INSERT INTO `");await command.ExecuteNonQueryAsync();
        }
        var healing=(await repository.LoadSelfHealingSkillAsync(112001))!;
        test("source Tiny healing resolves as a skill not potion",healing is {Amount:15,Skill.Mp:5,Skill.CastMs:1900}&&await repository.LoadPotionAsync(112001) is null);
        command.CommandText="INSERT INTO USERDATA(strUserID,Hp,Mp,Gold,strItem,strSerial) VALUES('heal-test',50,50,123,X'010203',X'040506')";await command.ExecuteNonQueryAsync();
        var healedHp=HealingRules.SelfHp(healing,50,100)!.Value;
        test("heal HP and MP cost commit in one transaction",await repository.SpendCombatSkillAsync("heal-test",healing.Skill,50,50,healedHp,50)==InventorySaveResult.Saved);
        command.CommandText="SELECT Hp,Mp,Gold,HEX(strItem) FROM USERDATA WHERE strUserID='heal-test'";
        await using(var r=await command.ExecuteReaderAsync()){await r.ReadAsync();test("healing persists HP/MP without consuming items or coins",Convert.ToInt32(r[0])==65&&Convert.ToInt32(r[1])==45&&Convert.ToInt32(r[2])==123&&Convert.ToString(r[3])=="010203");}
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
        foreach (var table in new[] { "ZONE_INFO","START_POSITION","K_NPC","K_NPCPOS","K_OBJECTPOS","K_MONSTER","K_MONSTER_ITEM","MAKE_ITEM_GROUP","MAKE_ITEM_GRADECODE","MAKE_WEAPON","MAKE_DEFENSIVE","MAKE_ITEM_LARECODE" })
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
        await repository.EnsureTravelSchemaAsync();
        command.CommandText="SELECT strZoneName FROM ZONE_INFO WHERE ServerNo=1 AND ZoneNo=21";
        test("Moradon: legacy map reference upgrades to packaged expanded geometry",Convert.ToString(await command.ExecuteScalarAsync())=="moradon_expanded.smd");
        command.CommandText="UPDATE ZONE_INFO SET strZoneName='custom.smd' WHERE ServerNo=1 AND ZoneNo=21";
        await command.ExecuteNonQueryAsync();
        await repository.EnsureMoradonNpcLayoutAsync();
        command.CommandText="SELECT COUNT(*) FROM KO_MORADON_NPCPOS_BACKUP";
        test("Moradon NPC layout: custom maps are not modified",Convert.ToInt32(await command.ExecuteScalarAsync())==0);
        command.CommandText="UPDATE ZONE_INFO SET strZoneName='moradon_expanded.smd' WHERE ServerNo=1 AND ZoneNo=21";
        await command.ExecuteNonQueryAsync();
        command.CommandText="CREATE TRIGGER reject_moradon_layout BEFORE INSERT ON K_NPCPOS FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='layout rollback test'";
        await command.ExecuteNonQueryAsync();
        var rolledBack=false;
        try { await repository.EnsureMoradonNpcLayoutAsync(); }
        catch(MySqlException ex) when(ex.Number==1644) { rolledBack=true; }
        command.CommandText="SELECT SUM(NumNPC) FROM K_NPCPOS WHERE ZoneID=21 AND ActType>=100";
        test("Moradon NPC layout: failed replacement rolls back deletes",rolledBack&&Convert.ToInt32(await command.ExecuteScalarAsync())==57);
        command.CommandText="SELECT COUNT(*) FROM KO_MORADON_NPCPOS_BACKUP";
        test("Moradon NPC layout: failed replacement rolls back backups",Convert.ToInt32(await command.ExecuteScalarAsync())==0);
        command.CommandText="DROP TRIGGER reject_moradon_layout";
        await command.ExecuteNonQueryAsync();
        await Task.WhenAll(repository.EnsureMoradonNpcLayoutAsync(),repository.EnsureMoradonNpcLayoutAsync());
        command.CommandText="SELECT SUM(NumNPC) FROM KO_MORADON_NPCPOS_BACKUP";
        test("Moradon NPC layout: concurrent startup backs up original placements once",Convert.ToInt32(await command.ExecuteScalarAsync())==57);
        command.CommandText="SELECT COUNT(*) FROM KO_MORADON_OBJECTPOS_BACKUP";
        test("Moradon NPC layout: original gate/anvil coordinates backed up",Convert.ToInt32(await command.ExecuteScalarAsync())==3);
        var expandedWorld = new WorldCatalog(await repository.LoadWorldDataAsync(),WorldCatalog.FindMapDirectory());
        var movedNpcs = expandedWorld.Zones[21].Npcs;
        test("Moradon NPC layout: native world has moved Inn and unchanged monsters",movedNpcs.Count(n=>n.Prototype.IsMonster)==474&&movedNpcs.Any(n=>n.Prototype.Id==16096&&n.X==770&&n.Z==648)&&movedNpcs.Count(n=>!n.Prototype.IsMonster)==45);
        command.CommandText="UPDATE K_NPCPOS SET LeftX=771,RightX=771 WHERE ZoneID=21 AND NpcID=16096 AND ActType>=100";
        await command.ExecuteNonQueryAsync();
        await repository.EnsureMoradonNpcLayoutAsync();
        command.CommandText="SELECT LeftX FROM K_NPCPOS WHERE ZoneID=21 AND NpcID=16096 AND ActType>=100";
        test("Moradon NPC layout: future operator edits survive restart",Convert.ToInt32(await command.ExecuteScalarAsync())==771);
        test("Moradon remaining NPCs: Clepio and statues move, guards retain count",movedNpcs.Any(n=>n.Prototype.Id==13003&&n.X==849&&n.Z==570)&&movedNpcs.Any(n=>n.Prototype.Id==16073&&n.X==790&&n.Z==539)&&movedNpcs.Count(n=>n.Prototype.Id==11020)==6);
        // Simulate an installation with v1 already applied and v2 not yet installed.
        command.CommandText="DELETE FROM KO_MORADON_LAYOUT WHERE LayoutId=2; DELETE FROM KO_MORADON_V2_NPCPOS_BACKUP; DELETE FROM K_NPCPOS WHERE ZoneID=21 AND ActType>=100 AND NpcID IN (521,11020,13003,16073,16074,21020); INSERT INTO K_NPCPOS SELECT * FROM KO_MORADON_NPCPOS_BACKUP WHERE NpcID IN (521,11020,13003,16073,16074,21020)";
        await command.ExecuteNonQueryAsync();
        await repository.EnsureMoradonNpcLayoutAsync();
        command.CommandText="SELECT LeftX FROM K_NPCPOS WHERE ZoneID=21 AND NpcID=13003 AND ActType>=100";
        test("Moradon v2 upgrades a previously applied v1 installation",Convert.ToInt32(await command.ExecuteScalarAsync())==849);
        command.CommandText="SELECT LeftX FROM K_NPCPOS WHERE ZoneID=21 AND NpcID=16096 AND ActType>=100";
        test("Moradon v2 never reapplies v1 or resets its operator edits",Convert.ToInt32(await command.ExecuteScalarAsync())==771);
        command.CommandText="SELECT SUM(NumNPC) FROM KO_MORADON_NPCPOS_BACKUP";
        test("Moradon v2 preserves the original v1 rollback snapshot",Convert.ToInt32(await command.ExecuteScalarAsync())==57);
        var nativeTown = (await repository.LoadTravelDestinationsAsync(1,80,21)).Single();
        nativeTown = TravelRules.TownForMap(nativeTown,expandedWorld.Zones[21].Map.Extent);
        test("Moradon: native Town now resolves confirmed plaza with loaded map",nativeTown.Px==81600&&nativeTown.Pz==54300&&expandedWorld.Zones[21].Map.Contains(816,543)&&float.IsFinite(expandedWorld.Zones[21].Map.Height(816,543)));
        await repository.EnsureTravelSchemaAsync();
        command.CommandText="UPDATE ZONE_INFO SET strZoneName='moradon_20060124.smd' WHERE ServerNo=1 AND ZoneNo=2";
        await command.ExecuteNonQueryAsync();
        await repository.EnsureTravelSchemaAsync();
        command.CommandText="SELECT strZoneName FROM ZONE_INFO WHERE ServerNo=1 AND ZoneNo=2";
        test("Moradon map reference repair never rewrites other zones",Convert.ToString(await command.ExecuteScalarAsync())=="moradon_20060124.smd");
        command.CommandText="UPDATE ZONE_INFO SET strZoneName='elmo_051221.smd' WHERE ServerNo=1 AND ZoneNo=2";
        await command.ExecuteNonQueryAsync();
        var oldPosition = new SavedPosition(21,30000,35000,0);
        var walked = new SavedPosition(21,25080,29100,-650);
        test("walking position persists independently of rewards", await repository.TrySavePositionAsync("combat-test",oldPosition,walked));
        test("stale logout cannot undo newer position", !await repository.TrySavePositionAsync("combat-test",oldPosition,new(21,1000,1000,0)));
        var nextWalk = walked with { X=26000 };
        var writes = await Task.WhenAll(repository.TrySavePositionAsync("combat-test",walked,nextWalk),repository.TrySavePositionAsync("combat-test",walked,walked with { X=27000 }));
        test("concurrent position snapshots allow only one writer", writes.Count(ok => ok) == 1);
        command.CommandText="SELECT Zone,PX,PZ,PY,Gold,Exp,strItem,strSerial,Hp,Mp FROM USERDATA WHERE strUserID='combat-test'";
        SavedPosition saved;
        await using (var reader = await command.ExecuteReaderAsync())
        {
            await reader.ReadAsync(); saved=new(Convert.ToUInt16(reader[0]),Convert.ToInt32(reader[1]),Convert.ToInt32(reader[2]),Convert.ToInt32(reader[3]));
            test("reconnect location is saved movement with unchanged inventory/resources", saved.X is 26000 or 27000 && saved.Z == 29100 && saved.Y == -650 && Convert.ToInt32(reader[4]) == 107 && ((byte[])reader[6]).SequenceEqual(after.Items) && ((byte[])reader[7]).SequenceEqual(after.Serials) && Convert.ToInt32(reader[8]) == 100 && Convert.ToInt32(reader[9]) == 50);
        }
        command.CommandText="SELECT COUNT(*) FROM KO_WORLD_TRAVEL WHERE strUserID='combat-test'";
        test("movement saves opt out of legacy spawn relocation", Convert.ToInt32(await command.ExecuteScalarAsync()) == 1);
        command.CommandText="ALTER TABLE USERDATA ADD CONSTRAINT fail_position CHECK (PX<>28000)"; await command.ExecuteNonQueryAsync();
        var positionFailed=false;
        try { await repository.TrySavePositionAsync("combat-test",saved,saved with { X=28000 }); } catch (MySqlException) { positionFailed=true; }
        command.CommandText="ALTER TABLE USERDATA DROP CONSTRAINT fail_position"; await command.ExecuteNonQueryAsync();
        test("position SQL failure rolls back and can retry", positionFailed && await repository.TrySavePositionAsync("combat-test",saved,saved with { X=29000 }));
        command.CommandText="ALTER TABLE USERDATA ADD Race tinyint unsigned DEFAULT 12,ADD HairRGB tinyint unsigned DEFAULT 2,ADD Face tinyint unsigned DEFAULT 1; INSERT INTO ACCOUNT_CHAR VALUES('preview-account','combat-test','','')";
        await command.ExecuteNonQueryAsync();
        // Seed physical inventory positions, independently of the preview mapping.
        // Reproduce the crashing log's distinct armor/weapon IDs and durabilities.
        var geared = slots.ToArray();
        geared[1]=new(216003011,23625,1,1001,0); // HEAD
        geared[4]=new(216001011,23625,1,1004,0); // BREAST
        geared[5]=ItemSlotData.Empty;            // SHOULDER
        geared[6]=new(146211041,18000,1,1006,0); // RIGHTHAND
        geared[8]=ItemSlotData.Empty;            // LEFTHAND
        geared[10]=new(216002011,23625,1,1010,0); // LEG
        geared[12]=new(216004011,23625,1,1012,0); // GLOVE
        geared[13]=new(216005011,23625,1,1013,0); // FOOT
        var equippedBlobs = ItemBlobs.Serialize(geared);
        var equipped = new InventorySnapshot(equippedBlobs.Items,equippedBlobs.Serials,107);
        test("equipped state persists before character list refresh", await repository.TrySaveInventoryAsync("combat-test",after,equipped));
        var preview = (await repository.LoadCharacterSelectInfoAsync("preview-account"))[0];
        var expectedPreview = new EquippedItem[] { new(216003011,23625),new(216001011,23625),new(0,0),new(146211041,18000),new(0,0),new(216002011,23625),new(216004011,23625),new(216005011,23625) };
        test("character select follows original ascending-slot wire order, weapons before legs", preview.VisibleEquipment.SequenceEqual(expectedPreview));
        var previewPacket = CharacterListPackets.Build([preview]);
        // Literal vector from CDBAgent::LoadCharInfo's ascending inventory loop;
        // unlike the old test, the expected order is not copied into blob seeding.
        test("crash-log equipment serializes to original C++ character-preview vector", Convert.ToHexString(previewPacket.Payload.Slice(21,48)) == "C3F1DF0C495CF3E9DF0C495C000000000000E100B7085046000000000000DBEDDF0C495CABF5DF0C495C93F9DF0C495C");
        geared[1]=ItemSlotData.Empty;
        var unequippedBlobs=ItemBlobs.Serialize(geared);
        test("unequip persists without retaining cached preview gear", await repository.TrySaveInventoryAsync("combat-test",equipped,new(unequippedBlobs.Items,unequippedBlobs.Serials,107)) && (await repository.LoadCharacterSelectInfoAsync("preview-account"))[0].VisibleEquipment[0].ItemId == 0);



    }
}
