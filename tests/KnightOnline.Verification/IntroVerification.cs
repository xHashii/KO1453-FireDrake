using System.Text.RegularExpressions;
using KnightOnline.Database;
using KnightOnline.Shared.Protocol;
using MySqlConnector;
namespace KnightOnline.Verification;
internal static class IntroVerification
{
    public static void Rules(Action<string,bool> test)
    {
        var flow = new WorldEntryFlow();
        test("intro: stage two before snapshot cannot enter", flow.Accept(2)==WorldEntryAction.Ignore);
        test("intro: first stage one bootstraps exactly once", flow.Accept(1)==WorldEntryAction.Bootstrap);
        test("intro: duplicate stage one only acknowledges", flow.Accept(1)==WorldEntryAction.AcknowledgeOnly);
        test("intro: stage two enters exactly once", flow.Accept(2)==WorldEntryAction.Enter && flow.Accept(2)==WorldEntryAction.Ignore);
        test("intro: late duplicate cannot replay MYINFO or quests", flow.Accept(1)==WorldEntryAction.AcknowledgeOnly);
        test("intro: unknown stages ignored", flow.Accept(255)==WorldEntryAction.Ignore);
        foreach(byte state in new byte[]{0,1,2,3,4})
        {
            var prepared = StarterQuestState.SuppressUnsupportedIntro([0xf4,1,state],1);
            test("intro: reserved flag completes from state "+state,prepared.Blob[2]==2 && prepared.Changed==(state!=2));
        }
        var snapshot=StarterQuestState.Snapshot(Convert.FromHexString("F40102330003"),2);
        test("intro: native snapshot sorted and contains completed intro",Convert.ToHexString(snapshot.Payload)=="010200330003F40102");
        bool Refuses(byte[] blob,short count) {try{StarterQuestState.SuppressUnsupportedIntro(blob,count);return false;}catch(InvalidDataException){return true;}}
        test("intro: malformed counts and duplicate identities refuse without rewriting",Refuses([],1)&&Refuses([], -1)&&Refuses(new byte[603],201)&&Refuses(Convert.FromHexString("F40101F40102"),2));
        var missingRejected=false;try{StarterQuestState.Snapshot([],0);}catch(InvalidDataException){missingRejected=true;}
        test("intro: sender never reseeds a missing flag as accepted",missingRejected);
    }
    public static async Task DatabaseAsync(DatabaseSettings settings,Action<string,bool> test)
    {
        // Runs LAST in the disposable ko_shop_test DB, never a player's database.
        // Exercise the actual public LoadUserData path, not merely the pure helper.
        await using var c=new MySqlConnection(settings.BuildConnectionString());await c.OpenAsync();await using var cmd=c.CreateCommand();
        var schema=await File.ReadAllTextAsync("sql/mysql/kn_online/0001_schema.sql");
        var ddl=Regex.Match(schema,@"CREATE TABLE `USERDATA`\([\s\S]*?\) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;").Value;
        if(ddl.Length==0)throw new InvalidDataException("Missing real USERDATA fixture DDL.");
        cmd.CommandText="DROP TABLE USERDATA; "+ddl+" INSERT INTO ACCOUNT_CHAR(strAccountID,strCharID1,strCharID2,strCharID3) VALUES('intro-account','intro-test','',''); INSERT INTO WAREHOUSE(strAccountID,nMoney,dwTime,WarehouseData,strSerial) VALUES('intro-account',0,0,X'00',X'00')";
        await cmd.ExecuteNonQueryAsync();
        var required=Regex.Matches(ddl,@"(?m)^ `([^`]+)` ([^\r\n]+)").Cast<Match>().Where(m=>m.Groups[2].Value.Contains("NOT NULL")).ToArray();
        cmd.CommandText="INSERT INTO USERDATA ("+string.Join(",",required.Select(m=>"`"+m.Groups[1].Value+"`"))+") VALUES ("+string.Join(",",required.Select(m=>m.Groups[1].Value=="strUserID"?"'intro-test'":m.Groups[2].Value.StartsWith("datetime")?"UTC_TIMESTAMP()":"0"))+")";
        await cmd.ExecuteNonQueryAsync();
        cmd.CommandText="UPDATE USERDATA SET Level=1,Zone=21,Gold=123456,Hp=17,Mp=11,strItem=X'010203',strSerial=X'040506',strSkill=X'050607',sQuestCount=2,strQuest=X'330003F40101000000' WHERE strUserID='intro-test'";await cmd.ExecuteNonQueryAsync();
        var repo=new MariaDbCharacterRepository(settings);
        var first=await repo.LoadUserDataAsync("intro-account","intro-test");
        test("intro DB: level-one login loads persisted completed intro and preserves unrelated quest/tail",first is not null&&first.QuestCount==2&&Convert.ToHexString(first.Quests)=="330003F40102000000");
        var again=await new MariaDbCharacterRepository(settings).LoadUserDataAsync("intro-account","intro-test");
        test("intro DB: reconnect yields same completed flag in native snapshot",again is not null&&Convert.ToHexString(StarterQuestState.Snapshot(again.Quests,again.QuestCount).Payload)=="010200330003F40102");
        test("intro DB: inventory, serials, skills, coins and vitals untouched",again is not null&&Convert.ToHexString(again.Items)=="010203"&&Convert.ToHexString(again.Serials!)=="040506"&&Convert.ToHexString(again.Skill)=="050607"&&again.Gold==123456&&again.Hp==17&&again.Mp==11);
        cmd.CommandText="UPDATE USERDATA SET Level=80,sQuestCount=0,strQuest=NULL WHERE strUserID='intro-test'";await cmd.ExecuteNonQueryAsync();
        var boosted=await repo.LoadUserDataAsync("intro-account","intro-test");
        test("intro DB: missing boosted intro seeded complete before snapshot",boosted is not null&&boosted.QuestCount==1&&Convert.ToHexString(boosted.Quests)=="F40102");
        cmd.CommandText="UPDATE USERDATA SET sQuestCount=2,strQuest=X'F40101' WHERE strUserID='intro-test'";await cmd.ExecuteNonQueryAsync();
        var refused=false;try{await repo.LoadUserDataAsync("intro-account","intro-test");}catch(InvalidDataException){refused=true;}
        cmd.CommandText="SELECT HEX(strQuest) FROM USERDATA WHERE strUserID='intro-test'";
        test("intro DB: corrupt count refused without quest repair writes",refused&&Convert.ToString(await cmd.ExecuteScalarAsync())=="F40101");
    }
}
