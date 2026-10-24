using System.IO.Compression;
using System.Text.RegularExpressions;
using KnightOnline.Database;
using KnightOnline.Database.Items;
using MySqlConnector;

namespace KnightOnline.Verification;

internal static class ItemCatalogVerification
{
    public static async Task RunAsync(DatabaseSettings settings, Action<string, bool> test)
    {
        await using var connection = new MySqlConnection(settings.BuildConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        // Exercise the ACTUAL shipped, index-free schema, not a simplified mock.
        var schema = await File.ReadAllTextAsync("sql/mysql/kn_online/0001_schema.sql");
        var ddl = Regex.Match(schema, @"CREATE TABLE `ITEM`\([\s\S]*?\) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;").Value;
        if (ddl.Length == 0) throw new InvalidDataException("ITEM fixture schema not found");
        command.CommandText = "DROP TABLE ITEM; " + ddl;
        await command.ExecuteNonQueryAsync();
        using (var source = typeof(MariaDbCharacterRepository).Assembly.GetManifestResourceStream("KnightOnline.Database.Items.ItemCatalog.sql.gz")!)
        using (var gzip = new GZipStream(source, CompressionMode.Decompress))
        using (var reader = new StreamReader(gzip))
        {
            var columns = await reader.ReadLineAsync();
            while (await reader.ReadLineAsync() is { } row)
                if (row.StartsWith("(120810001,"))
                {
                    command.CommandText = $"INSERT INTO ITEM {columns} VALUES {row}; UPDATE ITEM SET BuyPrice=123 WHERE Num=120810001";
                    await command.ExecuteNonQueryAsync();
                    break;
                }
        }
        var repository = new MariaDbCharacterRepository(settings);
        var first = await repository.RepairItemCatalogAsync();
        test("catalog restores ALL categories from partial import", first.SourceCount == 102884 && first.Restored == first.SourceCount - 1);
        test("catalog preserves custom definition instead of overwriting", (await repository.LoadShopItemAsync(120810001))!.BuyPrice == 123);
        test("catalog repeat startup is idempotent", (await repository.RepairItemCatalogAsync()).Restored == 0);
        command.CommandText = "DELETE FROM ITEM WHERE Num IN (389020000,389014000)";
        await command.ExecuteNonQueryAsync();
        var concurrent = await Task.WhenAll(repository.RepairItemCatalogAsync(), repository.RepairItemCatalogAsync());
        test("catalog concurrent startups restore missing IDs only once", concurrent.Sum(result => result.Restored) == 2);
        command.CommandText = "SELECT COUNT(*) FROM ITEM WHERE Num IN (389020000,389014000)";
        test("catalog adds no duplicate definitions", Convert.ToInt32(await command.ExecuteScalarAsync()) == 2);
        command.CommandText = "DELETE FROM ITEM WHERE Num IN (389020000,201001001); ALTER TABLE ITEM ADD CONSTRAINT inject_catalog_failure CHECK (Num<>389020000)";
        await command.ExecuteNonQueryAsync();
        var failed = false;
        try { await repository.RepairItemCatalogAsync(); } catch (MySqlException) { failed = true; }
        test("catalog failed batch rolls back without partial inserts", failed && await repository.LoadShopItemAsync(201001001) is null && await repository.LoadShopItemAsync(389020000) is null);
        command.CommandText = "ALTER TABLE ITEM DROP CONSTRAINT inject_catalog_failure";
        await command.ExecuteNonQueryAsync();
        test("catalog lock released after failure; repair can retry", (await repository.RepairItemCatalogAsync()).Restored == 2);

        var slots = Enumerable.Repeat(ItemSlotData.Empty, InventorySlots.Total).ToArray();
        var blobs = ItemBlobs.Serialize(slots);
        var snapshot = new InventorySnapshot(blobs.Items, blobs.Serials, 10000000);
        command.CommandText = "INSERT INTO USERDATA VALUES ('catalog-buyer',@gold,@items,@serials)";
        command.Parameters.AddWithValue("@gold", snapshot.Gold);
        command.Parameters.AddWithValue("@items", snapshot.Items);
        command.Parameters.AddWithValue("@serials", snapshot.Serials);
        await command.ExecuteNonQueryAsync();
        foreach (var (id, group, label) in new (uint, int, string)[]
        {
            (389020000,253000,"mana potion"), (389014000,253000,"health potion"),
            (120810001,201001,"weapon"), (201001001,202001,"armor"), (379001000,255000,"sundries")
        })
        {
            var item = (await repository.LoadShopItemAsync(id))!;
            var protos = await repository.LoadItemPrototypesAsync(slots.Select(s => s.ItemId).Append(id));
            var audit = ShopRules.MeasureInventory(slots, protos);
            var position = (byte)Array.FindIndex(slots, InventorySlots.InventoryStart, InventorySlots.InventoryCount, s => s.ItemId == 0);
            position -= InventorySlots.InventoryStart;
            var request = new ShopRequest(1, group, 10001, id, position, 1);
            var proposed = ShopRules.Propose(request, item, slots, snapshot.Gold, audit.Weight, 32767, id, out var error);
            test($"catalog {label}: authoritative item/prototype and group accepted", audit.Issues.Count == 0 && proposed is not null && error == 0);
            if (proposed is null) continue;
            var serialized = ItemBlobs.Serialize(proposed.Slots);
            var next = new InventorySnapshot(serialized.Items, serialized.Serials, proposed.Gold);
            test($"catalog {label}: purchase persisted atomically", await repository.TrySaveInventoryAsync("catalog-buyer", snapshot, next));
            await using var reload = connection.CreateCommand();
            reload.CommandText = "SELECT Gold,strItem,strSerial FROM USERDATA WHERE strUserID='catalog-buyer'";
            await using var row = await reload.ExecuteReaderAsync();
            await row.ReadAsync();
            slots = ItemBlobs.Parse((byte[])row.GetValue(1), (byte[])row.GetValue(2), Array.Empty<byte>(), InventorySlots.Total);
            test($"catalog {label}: reconnect state preserves item, count, durability, serial and price", row.GetInt32(0) == snapshot.Gold - item.BuyPrice && slots[14 + position] == proposed.Slots[14 + position]);
            snapshot = next;
        }
    }
}
