using System.Globalization;
using KnightOnline.Database;
using KnightOnline.Database.World;
using MySqlConnector;

namespace KnightOnline.Verification;

internal static class MoradonExactImportVerification
{
    public static async Task DatabaseAsync(MariaDbCharacterRepository repo,MySqlConnection connection,Action<string,bool> test)
    {
        await using var command=connection.CreateCommand();
        async Task Execute(string sql) { command.CommandText=sql;await command.ExecuteNonQueryAsync(); }
        async Task<int> Number(string sql) { command.CommandText=sql;return Convert.ToInt32(await command.ExecuteScalarAsync()); }
        async Task<string[]> Snapshot(string sql)
        {
            command.CommandText=sql;await using var reader=await command.ExecuteReaderAsync();var rows=new List<string>();
            while(await reader.ReadAsync())
            {
                var values=new object[reader.FieldCount];reader.GetValues(values);
                rows.Add(string.Join("\u001f",values.Select(v=>v switch {DBNull=>"<NULL>",byte[] b=>Convert.ToHexString(b),IFormattable f=>f.ToString(null,CultureInfo.InvariantCulture),_=>v.ToString()})));
            }
            return rows.Order(StringComparer.Ordinal).ToArray();
        }
        static bool Equal(string[] a,string[] b)=>a.AsSpan().SequenceEqual(b);
        static string[] Outside(WorldCatalog world)=>world.Zones.Values.Where(z=>z.Id!=21).SelectMany(z=>z.Npcs.Select(n=>$"{z.Id}:{n.Prototype}:{n.SpawnX}:{n.SpawnY}:{n.SpawnZ}")).ToArray();
        var npcDefs=await Snapshot("SELECT * FROM K_NPC");var monsterDefs=await Snapshot("SELECT * FROM K_MONSTER");
        var chars=await Snapshot("SELECT * FROM USERDATA");
        var outside=await Snapshot("SELECT * FROM K_NPCPOS WHERE ZoneID<>21");var outsideObjects=await Snapshot("SELECT * FROM K_OBJECTPOS WHERE ZoneID<>21");
        var oldSpawns=await Snapshot("SELECT * FROM K_NPCPOS WHERE ZoneID=21");var oldObjects=await Snapshot("SELECT * FROM K_OBJECTPOS WHERE ZoneID=21");
        var beforeWorld=new WorldCatalog(await repo.LoadWorldDataAsync(),WorldCatalog.FindMapDirectory());
        await Execute("UPDATE ZONE_INFO SET strZoneName='custom.smd' WHERE ServerNo=1 AND ZoneNo=21");
        await repo.EnsureMoradonExactImportAsync();
        test("exact Moradon import: custom map skipped",await Number("SELECT COUNT(*) FROM KO_MORADON_PREIMPORT_NPCPOS")==0);
        await Execute("UPDATE ZONE_INFO SET strZoneName='moradon_expanded.smd' WHERE ServerNo=1 AND ZoneNo=21");
        await Execute("CREATE TRIGGER reject_exact_import BEFORE INSERT ON K_NPCPOS FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='import rollback test'");
        var failed=false;
        try { await repo.EnsureMoradonExactImportAsync(); } catch(MySqlException ex) when(ex.Number==1644) { failed=true; }
        test("exact Moradon import: failed spawn insert rolls back definitions, objects and backups",failed&&await Number("SELECT COUNT(*) FROM KO_MORADON_IMPORTED_NPC")==0&&await Number("SELECT COUNT(*) FROM KO_MORADON_IMPORTED_MONSTER")==0&&await Number("SELECT COUNT(*) FROM KO_MORADON_PREIMPORT_NPCPOS")==0&&Equal(oldSpawns,await Snapshot("SELECT * FROM K_NPCPOS WHERE ZoneID=21"))&&Equal(oldObjects,await Snapshot("SELECT * FROM K_OBJECTPOS WHERE ZoneID=21")));
        await Execute("DROP TRIGGER reject_exact_import");
        await Task.WhenAll(repo.EnsureMoradonExactImportAsync(),repo.EnsureMoradonExactImportAsync());
        test("exact Moradon import: concurrent starts preserve one full prior snapshot",Equal(oldSpawns,await Snapshot("SELECT * FROM KO_MORADON_PREIMPORT_NPCPOS"))&&Equal(oldObjects,await Snapshot("SELECT * FROM KO_MORADON_PREIMPORT_OBJECTPOS")));
        test("exact Moradon import: latest owner archive replaces approximations",await Number("SELECT SUM(NumNPC) FROM K_NPCPOS WHERE ZoneID=21 AND ActType<100")==375&&await Number("SELECT SUM(NumNPC) FROM K_NPCPOS WHERE ZoneID=21 AND ActType>=100")==48&&await Number("SELECT COUNT(*) FROM K_OBJECTPOS WHERE ZoneID=21")==33);
        var data=await repo.LoadWorldDataAsync();var world=new WorldCatalog(data,WorldCatalog.FindMapDirectory());var zone=world.Zones[21];
        test("exact Moradon import: scoped definitions actually load",data.MoradonPrototypes?.Count==39&&data.MoradonMonsters?.Count==37&&zone.Npcs.Count(n=>n.Prototype.IsMonster)==375&&zone.Npcs.Count(n=>!n.Prototype.IsMonster)==49);
        test("exact Moradon import: new trainer/rental/dummy NPCs instantiated at source positions",zone.Npcs.Any(n=>n.Prototype.Id==13016&&n.X==887&&n.Z==477&&n.Prototype.Type==98)&&zone.Npcs.Any(n=>n.Prototype.Id==14401&&n.X==816&&n.Z==626)&&zone.Npcs.Count(n=>n.Prototype.Id==19067)==2);
        test("exact Moradon import: new monster definitions need not exist globally",!data.Monsters!.ContainsKey(1058)&&zone.Npcs.Any(n=>n.Prototype.Id==1058&&n.Prototype.IsMonster));
        test("exact Moradon import: actor positions finite and private loot adapters resolve",zone.Npcs.All(n=>zone.Map.Contains(n.X,n.Z)&&float.IsFinite(n.Y))&&zone.Npcs.Where(n=>n.Prototype.IsMonster).All(n=>n.Prototype.DropGroup==0||world.Drops.Drops.ContainsKey(n.Prototype.DropGroup)));
        test("exact Moradon import: global definitions and other-zone rows unchanged",Equal(npcDefs,await Snapshot("SELECT * FROM K_NPC"))&&Equal(monsterDefs,await Snapshot("SELECT * FROM K_MONSTER"))&&Equal(outside,await Snapshot("SELECT * FROM K_NPCPOS WHERE ZoneID<>21"))&&Equal(outsideObjects,await Snapshot("SELECT * FROM K_OBJECTPOS WHERE ZoneID<>21")));
        test("exact Moradon import: other-zone runtime models/stats unchanged",Equal(Outside(beforeWorld),Outside(world)));
        test("exact Moradon import: saved character data untouched",Equal(chars,await Snapshot("SELECT * FROM USERDATA")));
        await Execute("UPDATE KO_MORADON_IMPORTED_NPC SET strName='Operator edit' WHERE sSid=13016");
        await repo.EnsureMoradonNpcLayoutAsync();await repo.EnsureMoradonMonsterLayoutAsync();await repo.EnsureMoradonExactImportAsync();
        command.CommandText="SELECT strName FROM KO_MORADON_IMPORTED_NPC WHERE sSid=13016";
        test("exact Moradon import: later startup preserves operator edits",Convert.ToString(await command.ExecuteScalarAsync())=="Operator edit");
        // Prove reversible without changing global catalogs or re-running migrations.
        await Execute("START TRANSACTION; DELETE FROM K_NPCPOS WHERE ZoneID=21; INSERT INTO K_NPCPOS SELECT * FROM KO_MORADON_PREIMPORT_NPCPOS; DELETE FROM K_OBJECTPOS WHERE ZoneID=21; INSERT INTO K_OBJECTPOS SELECT * FROM KO_MORADON_PREIMPORT_OBJECTPOS; DELETE FROM KO_MORADON_IMPORTED_NPC; DELETE FROM KO_MORADON_IMPORTED_MONSTER; COMMIT");
        var restoredData=await repo.LoadWorldDataAsync();
        test("exact Moradon import: rollback disables both private catalogs and restores prior spawns",restoredData.MoradonPrototypes is null&&restoredData.MoradonMonsters is null&&Equal(oldSpawns,await Snapshot("SELECT * FROM K_NPCPOS WHERE ZoneID=21")));
    }
}
