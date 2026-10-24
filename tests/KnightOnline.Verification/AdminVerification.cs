using KnightOnline.Database;
using KnightOnline.Database.Admin;
using KnightOnline.Database.Items;
using MySqlConnector;

namespace KnightOnline.Verification;

internal static class AdminVerification
{
    public static async Task RunAsync(DatabaseSettings settings, Action<string, bool> test)
    {
        await using var connection = new MySqlConnection(settings.BuildConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DROP TABLE IF EXISTS USERDATA,WAREHOUSE,ACCOUNT_CHAR,CURRENTUSER,K_MONSTER_ITEM,K_MONSTER; " +
            "CREATE TABLE USERDATA(strUserID varchar(20),Class smallint,Level tinyint unsigned,Gold int,strItem blob,strSerial blob) ENGINE=InnoDB; " +
            "CREATE TABLE ACCOUNT_CHAR(strAccountID varchar(20),strCharID1 varchar(20),strCharID2 varchar(20),strCharID3 varchar(20)) ENGINE=InnoDB; " +
            "INSERT INTO ACCOUNT_CHAR VALUES('admin-account','admin-test','sibling',''); " +
            "CREATE TABLE K_MONSTER(sSid smallint,strName varchar(40),sItem smallint); " +
            "INSERT INTO K_MONSTER VALUES(1,'Worm',1); " +
            "CREATE TABLE K_MONSTER_ITEM(sIndex smallint," + string.Join(",", Enumerable.Range(1,5).Select(i => $"iItem{i:00} int NULL,sPersent{i:00} smallint NULL")) + ") ENGINE=InnoDB; " +
            "INSERT INTO K_MONSTER_ITEM VALUES(1,0,0,0,0,389014000,100,NULL,NULL,0,0);";
        await command.ExecuteNonQueryAsync();
        command.CommandText = await File.ReadAllTextAsync("sql/mysql/kn_online/0004_admin_tables.sql");
        await command.ExecuteNonQueryAsync();
        var slots = Enumerable.Repeat(ItemSlotData.Empty, 75).ToArray();
        slots[74] = new(123, -123, 42, 999, 0);
        var blobs = ItemBlobs.Serialize(slots);
        command.CommandText = "INSERT INTO USERDATA VALUES('admin-test',101,1,100,@items,@serials),('sibling',101,1,100,@items,@serials)";
        command.Parameters.AddWithValue("@items", blobs.Items);
        command.Parameters.AddWithValue("@serials", blobs.Serials);
        await command.ExecuteNonQueryAsync();
        command.Parameters.Clear();
        var repo = new CharacterAdminRepository(settings.BuildConnectionString());
        var page = await repo.SearchCharactersAsync("", 1, offset: 1);
        test("admin empty search supports subsequent pages", page.Count == 1 && page[0].Name == "sibling");
        var character = (await repo.GetCharacterAsync("admin-test"))!;
        character.Gold = 200;
        character.ConcurrencyToken = await repo.SaveCharacterAsync(character, "verification");
        test("admin preserves stored slots beyond visible inventory", (await repo.GetCharacterAsync(character.Name))!.Inventory[74] == slots[74]);
        command.CommandText = "UPDATE USERDATA SET Gold=201 WHERE strUserID='admin-test'";
        await command.ExecuteNonQueryAsync();
        var rejected = false;
        try { await repo.SaveCharacterAsync(character, "verification"); } catch (AdminEditRejectedException) { rejected = true; }
        test("admin stale scalar-only change rejected", rejected);
        using (var lease = new MariaDbCharacterRepository(settings).AcquireEditLease("admin-account", "admin-test"))
        {
            test("admin online state reads live server lease", (await repo.GetCharacterAsync("admin-test"))!.IsOnline);
            rejected = false;
            try { await repo.SaveCharacterAsync((await repo.GetCharacterAsync("sibling"))!, "verification"); } catch (InvalidOperationException) { rejected = true; }
            test("admin cannot edit sibling warehouse/account during live session", rejected);
        }
        test("admin logout releases live state", !(await repo.GetCharacterAsync("admin-test"))!.IsOnline);
        var drop = (await repo.GetDropAsync(1))!;
        var stale = (await repo.GetDropAsync(1))!;
        drop.Chances[2] = 50; // Existing direct source item, lower probability.
        drop.Token = await repo.SaveDropAsync(drop, "verification");
        test("drop save preserves nullable slots", (await repo.GetDropAsync(1))!.Items[3] is null);
        rejected = false;
        try { await repo.SaveDropAsync(stale, "verification"); } catch (AdminEditRejectedException) { rejected = true; }
        test("drop stale edit rejected", rejected);
        drop.Chances[2] = 10001;
        rejected = false;
        try { await repo.SaveDropAsync(drop, "verification"); } catch (AdminEditRejectedException) { rejected = true; }
        test("drop invalid probability rejected", rejected);
        command.CommandText = "INSERT INTO K_MONSTER_ITEM SELECT * FROM K_MONSTER_ITEM WHERE sIndex=1";
        await command.ExecuteNonQueryAsync();
        rejected = false;
        try { await repo.GetDropAsync(1); } catch (AdminEditRejectedException) { rejected = true; }
        test("drop ambiguous duplicate ID rejected", rejected);
    }
}
