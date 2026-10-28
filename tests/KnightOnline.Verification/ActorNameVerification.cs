using System.Globalization;
using KnightOnline.Database;
using MySqlConnector;

namespace KnightOnline.Verification;

internal static class ActorNameVerification
{
    public static async Task DatabaseAsync(MariaDbCharacterRepository repo,MySqlConnection connection,Action<string,bool> test)
    {
        await using var command=connection.CreateCommand();
        async Task Exec(string sql) { command.CommandText=sql;await command.ExecuteNonQueryAsync(); }
        async Task<string> Text(string sql) { command.CommandText=sql;return Convert.ToString(await command.ExecuteScalarAsync())??""; }
        async Task<string[]> Snapshot(string table,bool omitName=false)
        {
            command.CommandText="SELECT * FROM "+table;await using var r=await command.ExecuteReaderAsync();var rows=new List<string>();
            while(await r.ReadAsync())
            {
                var values=new List<string>();
                for(var i=0;i<r.FieldCount;i++)
                {
                    if(omitName&&r.GetName(i)=="strName") continue;
                    var v=r.GetValue(i);values.Add(v switch{DBNull=>"<NULL>",byte[] b=>Convert.ToHexString(b),IFormattable f=>f.ToString(null,CultureInfo.InvariantCulture)??"",_=>v.ToString()??""});
                }
                rows.Add(string.Join("\u001f",values));
            }
            return rows.Order(StringComparer.Ordinal).ToArray();
        }
        static bool Equal(string[] a,string[] b)=>a.AsSpan().SequenceEqual(b);
        // Simulate an already-imported database with the clipped SQL name.
        await Exec("UPDATE KO_MORADON_IMPORTED_NPC SET strName='`Healing Potion`Karp' WHERE sSid=13005");
        var tables=new[]{"K_NPC","K_MONSTER","KO_MORADON_IMPORTED_NPC","KO_MORADON_IMPORTED_MONSTER","K_NPCPOS","K_OBJECTPOS","USERDATA"};
        var before=new Dictionary<string,string[]>();
        foreach(var table in tables) before[table]=await Snapshot(table,true);
        await Exec("CREATE TRIGGER reject_actor_names BEFORE UPDATE ON K_NPC FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='name rollback test'");
        var failed=false;
        try { await repo.RepairActorNamesAsync(); } catch(MySqlException ex) when(ex.Number==1644) { failed=true; }
        test("actor names: failed name update rolls back its backup",failed&&await Text("SELECT COUNT(*) FROM KO_ACTOR_NAME_BACKUP")=="0"&&await Text("SELECT strName FROM K_NPC WHERE sSid=14429")=="a");
        await Exec("DROP TRIGGER reject_actor_names");
        var repairs=await Task.WhenAll(repo.RepairActorNamesAsync(),repo.RepairActorNamesAsync());
        test("actor names: concurrent startup repairs damaged names once",repairs.Sum()==51&&await Text("SELECT COUNT(*) FROM KO_ACTOR_NAME_BACKUP")=="51");
        test("actor names: full imported shop and rental names restored",await Text("SELECT strName FROM KO_MORADON_IMPORTED_NPC WHERE sSid=13005")=="[Healing Potion]Karpis"&&await Text("SELECT strName FROM KO_MORADON_IMPORTED_NPC WHERE sSid=14402")=="[Novice Weapon Rental]Sodern");
        test("actor names: blank monsters and placeholder dragons restored",await Text("SELECT strName FROM K_MONSTER WHERE sSid=5801")=="Ego"&&await Text("SELECT strName FROM K_MONSTER WHERE sSid=6501")=="Ultima"&&await Text("SELECT strName FROM K_MONSTER WHERE sSid=7041")=="Dark Dragon");
        test("actor names: apostrophe not removed or mistaken for a role delimiter",await Text("SELECT strName FROM K_NPC WHERE sSid=16079")=="[Kaishan's Daughter] Menissiah");
        var clean=true;
        foreach(var table in tables.Take(4))
            clean &= await Text($"SELECT COUNT(*) FROM {table} WHERE INSTR(strName,CHAR(96))>0 OR TRIM(COALESCE(strName,'')) IN ('','a')")=="0";
        test("actor names: every loaded catalog audited free of known backticks/blanks/placeholders",clean);
        var unchanged=true;
        foreach(var table in tables) unchanged &= Equal(before[table],await Snapshot(table,true));
        test("actor names: stats, models, placements, objects and saved characters untouched",unchanged);
        test("actor names: custom operator name is preserved",await Text("SELECT strName FROM KO_MORADON_IMPORTED_NPC WHERE sSid=13016")=="Operator edit");
        test("actor names: repeated startup is idempotent",await repo.RepairActorNamesAsync()==0&&await Text("SELECT COUNT(*) FROM KO_ACTOR_NAME_BACKUP")=="51");
    }
}
