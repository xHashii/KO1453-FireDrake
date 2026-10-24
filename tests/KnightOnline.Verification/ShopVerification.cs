using KnightOnline.Database;
using KnightOnline.Database.Items;
using KnightOnline.Shared.Protocol;
using MySqlConnector;

namespace KnightOnline.Verification;

internal static class ShopVerification
{
    public static async Task RunAsync(Action<string, bool> check)
    {
        void Test(string name, bool condition) => check("shop: " + name, condition);
        var slots = Enumerable.Repeat(ItemSlotData.Empty, InventorySlots.Total).ToArray();
        var potion = new ShopItem(889001000, 120, 0, 253, 0, 1, 5, 1);
        var buy = new ShopRequest(1, 253000, 10001, potion.Id, 0, 5);
        ShopChange? Propose(ShopRequest r, ShopItem? item = null, int gold = 10000, long weight = 0, int max = 1000) =>
            ShopRules.Propose(r, item ?? potion, slots, gold, weight, max, 1234567, out _);
        var bought = Propose(buy)!;
        Test("buy charged and correct stack created", bought.Gold == 9400 && bought.Price == 600 && bought.Slots[14] == new ItemSlotData(potion.Id, 1, 5, 1234567, 0));
        Test("proposal leaves original inventory untouched", slots.All(s => s == ItemSlotData.Empty));
        slots = bought.Slots;
        Test("buy stacks with serial preserved", Propose(buy)!.Slots[14].Count == 10 && Propose(buy)!.Slots[14].Serial == 1234567);
        Test("insufficient coins", Propose(buy, gold: 599) is null);
        Test("exact funds accepted", Propose(buy, gold: 600)!.Gold == 0);
        Test("weight overflow rejected", Propose(buy, weight: 32760, max: 32767) is null);
        Test("exact max weight accepted", Propose(buy, weight: 975) is not null);
        Test("zero and excessive quantities rejected", Propose(buy with { Count = 0 }) is null && Propose(buy with { Count = 10000 }) is null);
        Test("unsigned max count rejected", Propose(buy with { Count = ushort.MaxValue }) is null);
        Test("bad position rejected", Propose(buy with { Position = 28 }) is null && Propose(buy with { Position = 255 }) is null);
        Test("negative price rejected", Propose(buy, potion with { BuyPrice = -1 }) is null);
        Test("price multiplication cannot wrap", Propose(buy, potion with { BuyPrice = int.MaxValue }) is null);
        Test("wrong shop and unsold item rejected", Propose(buy with { Group = 101000 }) is null && Propose(buy, potion with { SellingGroup = 0 }) is null);
        Test("legacy selling group variants", new[] { 253000, 253001, 253002, 253072 }.All(g => ShopRules.Sells(g, 253)) && !ShopRules.Sells(253003, 253));
        slots[14] = slots[14] with { Count = 9998 };
        Test("stack cap does not overflow", Propose(buy) is null);
        slots[14] = bought.Slots[14] with { Count = 5 }; // bought.Slots is the current array; restore explicitly
        slots[15] = new ItemSlotData(111, 20, 1, 999, 123);
        Test("buy cannot overwrite another item", Propose(buy with { Position = 1 }) is null);
        var sword = potion with { Countable = 0 };
        Test("non-stackable multi-buy/occupied slot rejected", Propose(buy with { Position = 2 }, sword) is null && Propose(buy with { Count = 1 }, sword) is null);
        Test("single equipment purchase accepted", Propose(buy with { Count = 1, Position = 2 }, sword) is not null);
        var sell = buy with { Type = 2, Count = 2 };
        var sold = Propose(sell)!;
        Test("partial sale uses BuyPrice/6 and keeps serial", sold.Gold == 10040 && sold.Price == 40 && sold.Slots[14].Count == 3 && sold.Slots[14].Serial == 1234567);
        Test("full sale clears whole slot", Propose(sell with { Count = 5 })!.Slots[14] == ItemSlotData.Empty);
        Test("sell over owned amount rejected", Propose(sell with { Count = 6 }) is null);
        Test("sale cannot exceed coin cap", Propose(sell, gold: ShopRules.CoinMax - 39) is null);
        Test("full-price sell type", Propose(sell, potion with { SellType = 1 })!.Price == 240);
        Test("untradeable race rejected", Propose(sell, potion with { Race = 20 }) is null);
        var restricted = potion with { Id = 900000001 };
        Test("restricted item ID rejected", Propose(sell with { ItemId = restricted.Id }, restricted) is null);
        Test("wrong source item rejected", Propose(sell with { Position = 1 }) is null);
        slots[14] = slots[14] with { ExpirationUnixTime = 123 };
        Test("timed items cannot be sold/merged", Propose(sell) is null && Propose(buy) is null);
        slots[14] = slots[14] with { ExpirationUnixTime = 0 };
        var move = new ShopRequest(3, 0, 0, potion.Id, 0, 0, 1);
        var moved = Propose(move)!;
        Test("shop move swaps complete records including serial/time", moved.Slots[15] == slots[14] && moved.Slots[14] == slots[15] && moved.Gold == 10000);
        Test("invalid shop move rejected", Propose(move with { Destination = 28 }) is null && Propose(move with { ItemId = 0 }) is null);
        Test("short inventory rejected", ShopRules.Propose(buy, potion, Array.Empty<ItemSlotData>(), 1000, 0, 1000, 1, out _) is null);
        Test("NPC range/type/group/faction validation", ShopRules.CanUseNpc(21, 253000, 253000, 11, 0, true) &&
            !ShopRules.CanUseNpc(21, 253000, 253000, 11, 1, true) && !ShopRules.CanUseNpc(24, 253000, 253000, 0, 0, true) &&
            !ShopRules.CanUseNpc(21, 253000, 254000, 0, 0, true) && !ShopRules.CanUseNpc(21, 253000, 253000, 0, 0, false));

        Packet Received(params byte[] bytes) => Packet.FromReceived(WizOpcode.ItemTrade, bytes);
        var raw = new Packet(WizOpcode.ItemTrade).WriteByte(1).WriteInt32(253000).WriteUInt16(10001).WriteUInt32(potion.Id).WriteByte(0).WriteUInt16(5);
        Test("buy request matches C++ layout", ShopRules.TryRead(Packet.FromReceived(raw.Opcode, raw.Payload), out var parsed) && parsed == buy);
        Test("truncated/trailing/unknown packets rejected", !ShopRules.TryRead(Received(), out _) && !ShopRules.TryRead(Received(1), out _) &&
            !ShopRules.TryRead(Received(4, 0, 0, 0, 0, 0, 0), out _) && !ShopRules.TryRead(Packet.FromReceived(raw.Opcode, raw.Payload.ToArray().Concat(new byte[] { 0 }).ToArray()), out _));
        Test("move request layout", ShopRules.TryRead(Received(3, 0x11, 0, 0, 0, 2, 3), out var parsedMove) && parsedMove == new ShopRequest(3, 0, 0, 17, 2, 0, 3));
        Test("success reply golden payload", Convert.ToHexString(ShopRules.Success(new ShopChange(slots, 9400, 600, 253), 1).Payload) == "01B824000058020000FD");
        Test("failure and move golden payloads", Convert.ToHexString(ShopRules.Failure(3).Payload) == "0003" && Convert.ToHexString(ShopRules.Success(moved, 3).Payload) == "03");

        if (Environment.GetEnvironmentVariable("KO_SHOP_TEST_PORT") is { } port)
            await DatabaseChecksAsync(uint.Parse(port), Test);
    }

    private static async Task DatabaseChecksAsync(uint port, Action<string, bool> test)
    {
        // Dedicated disposable CI database. Never points at a user's game database.
        var settings = new DatabaseSettings("127.0.0.1", port, "ko_shop_test", "root", "verification");
        await using var connection = new MySqlConnection(settings.BuildConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE USERDATA (strUserID varchar(21) PRIMARY KEY, Gold int NOT NULL,strItem blob,strSerial blob) ENGINE=InnoDB; " +
            "CREATE TABLE SEALED_ITEMS (strUserID varchar(21),nItemID int,nItemSerial bigint) ENGINE=InnoDB; " +
            "CREATE TABLE USER_RENTAL_ITEM (strUserID varchar(21),nItemIndex int,nSerialNumber bigint) ENGINE=InnoDB; " +
            "CREATE TABLE RENTAL_ITEM (strBorrowerCharID varchar(21),nItemIndex int,nSerialNumber bigint) ENGINE=InnoDB; " +
            "CREATE TABLE ITEM (Num int PRIMARY KEY,BuyPrice int,SellPrice int,SellingGroup tinyint unsigned,Race tinyint unsigned,Countable tinyint unsigned,Weight smallint,Duration smallint); " +
            "INSERT INTO ITEM VALUES (889001000,120,0,253,0,1,5,1); " +
            "INSERT INTO USERDATA VALUES ('shop-test',1000,X'010203',X'040506'),('null-test',1000,NULL,NULL);";
        await command.ExecuteNonQueryAsync();
        var repository = new MariaDbCharacterRepository(settings);
        test("DB authoritative shop metadata", await repository.LoadShopItemAsync(889001000) == new ShopItem(889001000, 120, 0, 253, 0, 1, 5, 1));
        test("DB missing shop item rejected", await repository.LoadShopItemAsync(999999) is null);
        test("DB null legacy blobs normalized", await repository.TrySaveInventoryAsync("null-test",
            new InventorySnapshot(Array.Empty<byte>(), Array.Empty<byte>(), 1000),
            new InventorySnapshot(new byte[] { 1 }, new byte[] { 2 }, 900)));
        var before = new InventorySnapshot(new byte[] { 1, 2, 3 }, new byte[] { 4, 5, 6 }, 1000);
        var after = new InventorySnapshot(new byte[] { 7, 8 }, new byte[] { 9, 10 }, 400);
        test("DB atomic commit succeeds", await repository.TrySaveInventoryAsync("shop-test", before, after));
        test("DB stale/replayed snapshot rejected", !await repository.TrySaveInventoryAsync("shop-test", before, after));
        var next = new InventorySnapshot(new byte[] { 11 }, new byte[] { 12 }, 500);
        test("DB wrong serial snapshot rejected", !await repository.TrySaveInventoryAsync("shop-test", after with { Serials = new byte[] { 99 } }, next));
        test("DB wrong gold snapshot rejected", !await repository.TrySaveInventoryAsync("shop-test", after with { Gold = 401 }, next));
        test("DB wrong item snapshot rejected", !await repository.TrySaveInventoryAsync("shop-test", after with { Items = new byte[] { 99 } }, next));
        test("DB missing character rejected", !await repository.TrySaveInventoryAsync("missing", after, next));
        command.CommandText = "INSERT INTO SEALED_ITEMS VALUES ('shop-test',111,123); INSERT INTO USER_RENTAL_ITEM VALUES ('shop-test',222,456); INSERT INTO RENTAL_ITEM VALUES ('shop-test',333,789);";
        await command.ExecuteNonQueryAsync();
        test("DB sealed sale/merge rejected", !await repository.TrySaveInventoryAsync("shop-test", after, next, 111, 123));
        test("DB rental sale/merge rejected", !await repository.TrySaveInventoryAsync("shop-test", after, next, 222, 456) && !await repository.TrySaveInventoryAsync("shop-test", after, next, 333, 789));
        command.CommandText = "INSERT INTO SEALED_ITEMS VALUES ('other-character',444,999)";
        await command.ExecuteNonQueryAsync();
        test("DB serial protection follows items across characters", !await repository.TrySaveInventoryAsync("shop-test", after, next, 444, 999));
        test("DB zero serial conservative protection", !await repository.TrySaveInventoryAsync("shop-test", after, next, 111, 0));
        command.CommandText = "SELECT Gold,strItem,strSerial FROM USERDATA WHERE strUserID='shop-test'";
        await using (var row = await command.ExecuteReaderAsync())
        {
            await row.ReadAsync();
            test("DB rejection leaves ALL fields unchanged/reload persists", row.GetInt32(0) == after.Gold && ((byte[])row.GetValue(1)).SequenceEqual(after.Items) && ((byte[])row.GetValue(2)).SequenceEqual(after.Serials));
        }
        var results = await Task.WhenAll(repository.TrySaveInventoryAsync("shop-test", after, next), repository.TrySaveInventoryAsync("shop-test", after, next));
        test("DB concurrent stale writers exactly one commits", results.Count(success => success) == 1);
        test("DB unchanged inventory swap can acknowledge success", await repository.TrySaveInventoryAsync("shop-test", next, next));
        command.CommandText = "ALTER TABLE USERDATA ADD CONSTRAINT test_gold CHECK (strUserID<>'shop-test' OR Gold=500)";
        await command.ExecuteNonQueryAsync();
        var threw = false;
        try { await repository.TrySaveInventoryAsync("shop-test", next, before); } catch (MySqlException) { threw = true; }
        test("DB SQL failure raised instead of success", threw);
        command.CommandText = "SELECT Gold,strItem,strSerial FROM USERDATA WHERE strUserID='shop-test'";
        await using (var row = await command.ExecuteReaderAsync())
        {
            await row.ReadAsync();
            test("DB SQL failure rolls back coins/items/serials together", row.GetInt32(0) == next.Gold && ((byte[])row.GetValue(1)).SequenceEqual(next.Items) && ((byte[])row.GetValue(2)).SequenceEqual(next.Serials));
        }
    }
}
