using System.Globalization;
using KnightOnline.Database;
using KnightOnline.Database.World;
using MySqlConnector;

namespace KnightOnline.Verification;

internal static class MoradonMonsterVerification
{
    public static async Task DatabaseAsync(MariaDbCharacterRepository repository, MySqlConnection connection, Action<string,bool> test)
    {
        await using var command=connection.CreateCommand();
        async Task Execute(string sql) { command.CommandText=sql; await command.ExecuteNonQueryAsync(); }
        async Task<int> Number(string sql) { command.CommandText=sql; return Convert.ToInt32(await command.ExecuteScalarAsync()); }
        async Task<string[]> Snapshot(string sql)
        {
            command.CommandText=sql;
            await using var reader=await command.ExecuteReaderAsync();
            var rows=new List<string>();
            while(await reader.ReadAsync())
            {
                var values=new object[reader.FieldCount];reader.GetValues(values);
                rows.Add(string.Join("\u001f",values.Select(v=>v switch
                {
                    DBNull=>"<NULL>", byte[] bytes=>Convert.ToHexString(bytes),
                    IFormattable f=>f.ToString(null,CultureInfo.InvariantCulture), _=>v.ToString()
                })));
            }
            return rows.Order(StringComparer.Ordinal).ToArray();
        }
        var outside=await Snapshot("SELECT * FROM K_NPCPOS WHERE ZoneID<>21 OR ActType>=100");
        var prototypes=await Snapshot("SELECT * FROM K_MONSTER");
        var objects=await Snapshot("SELECT * FROM K_OBJECTPOS");
        var characters=await Snapshot("SELECT * FROM USERDATA");
        var original=await Snapshot("SELECT * FROM K_NPCPOS WHERE ZoneID=21 AND ActType<100");
        await Execute("UPDATE ZONE_INFO SET strZoneName='custom.smd' WHERE ServerNo=1 AND ZoneNo=21");
        await repository.EnsureMoradonMonsterLayoutAsync();
        test("monster placement: custom maps skipped",original.SequenceEqual(await Snapshot("SELECT * FROM K_NPCPOS WHERE ZoneID=21 AND ActType<100"))&&await Number("SELECT COUNT(*) FROM KO_MORADON_MONSTERPOS_BACKUP")==0);
        await Execute("UPDATE ZONE_INFO SET strZoneName='moradon_expanded.smd' WHERE ServerNo=1 AND ZoneNo=21");
        await Execute("CREATE TRIGGER reject_moradon_monsters BEFORE INSERT ON K_NPCPOS FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='monster layout rollback test'");
        var failed=false;
        try { await repository.EnsureMoradonMonsterLayoutAsync(); }
        catch(MySqlException ex) when(ex.Number==1644) { failed=true; }
        test("monster placement: failed insert rolls back original monsters and snapshot",failed&&original.SequenceEqual(await Snapshot("SELECT * FROM K_NPCPOS WHERE ZoneID=21 AND ActType<100"))&&await Number("SELECT COUNT(*) FROM KO_MORADON_MONSTERPOS_BACKUP")==0);
        test("monster placement: failed transaction does not mark applied",await Number("SELECT COUNT(*) FROM KO_MORADON_LAYOUT WHERE LayoutId=4 AND AppliedUtc IS NULL")==1);
        await Execute("DROP TRIGGER reject_moradon_monsters");
        await Task.WhenAll(repository.EnsureMoradonMonsterLayoutAsync(),repository.EnsureMoradonMonsterLayoutAsync());
        test("monster placement: concurrent starts apply once with full original backup",original.SequenceEqual(await Snapshot("SELECT * FROM KO_MORADON_MONSTERPOS_BACKUP"))&&await Number("SELECT SUM(NumNPC) FROM KO_MORADON_MONSTERPOS_BACKUP")==474);
        test("monster placement: exact plus adapted rows populate expanded map",await Number("SELECT COUNT(*) FROM K_NPCPOS WHERE ZoneID=21 AND ActType<100")==195&&await Number("SELECT SUM(NumNPC) FROM K_NPCPOS WHERE ZoneID=21 AND ActType<100")==646);
        test("monster placement: other zones, NPCs, objects and monster definitions unchanged",outside.SequenceEqual(await Snapshot("SELECT * FROM K_NPCPOS WHERE ZoneID<>21 OR ActType>=100"))&&prototypes.SequenceEqual(await Snapshot("SELECT * FROM K_MONSTER"))&&objects.SequenceEqual(await Snapshot("SELECT * FROM K_OBJECTPOS")));
        test("monster placement: character positions, inventory and resources unchanged",characters.SequenceEqual(await Snapshot("SELECT * FROM USERDATA")));
        var world=new WorldCatalog(await repository.LoadWorldDataAsync(),WorldCatalog.FindMapDirectory());
        var zone=world.Zones[21];var monsters=zone.Npcs.Where(n=>n.Prototype.IsMonster).ToArray();
        test("monster placement: actual WorldCatalog loads all new points on finite terrain",monsters.Length==646&&monsters.All(n=>zone.Map.Contains(n.X,n.Z)&&float.IsFinite(n.Y))&&zone.Npcs.Count(n=>!n.Prototype.IsMonster)==45);
        test("monster placement: Kecoon field uses supplied new coordinates",monsters.Where(n=>n.Prototype.Id==150).Count()==5&&monsters.Where(n=>n.Prototype.Id==150).All(n=>n.X>=597&&n.X<=614&&n.Z>=326&&n.Z<=341&&n.RespawnSeconds==30));
        test("monster placement: legacy boss instances and respawn interval retained",monsters.Count(n=>n.Prototype.Id==161)==4&&monsters.Where(n=>n.Prototype.Id==161).All(n=>n.RespawnSeconds==32000));
        await Execute("UPDATE K_NPCPOS SET LeftX=LeftX+1,RightX=RightX+1 WHERE ZoneID=21 AND ActType<100 AND NpcID=750");
        var edited=await Snapshot("SELECT * FROM K_NPCPOS WHERE ZoneID=21 AND ActType<100");
        await repository.EnsureMoradonNpcLayoutAsync();
        await repository.EnsureMoradonMonsterLayoutAsync();
        test("monster placement: subsequent startup preserves operator edits and original backup",edited.SequenceEqual(await Snapshot("SELECT * FROM K_NPCPOS WHERE ZoneID=21 AND ActType<100"))&&original.SequenceEqual(await Snapshot("SELECT * FROM KO_MORADON_MONSTERPOS_BACKUP")));
    }
}
