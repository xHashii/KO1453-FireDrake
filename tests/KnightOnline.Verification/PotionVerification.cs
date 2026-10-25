using System.Text.RegularExpressions;
using KnightOnline.Database;
using KnightOnline.Database.Items;
using KnightOnline.Shared.Protocol;
using MySqlConnector;

namespace KnightOnline.Verification;

internal static class PotionVerification
{
    public static void Rules(Action<string, bool> test)
    {
        var slots = Enumerable.Repeat(ItemSlotData.Empty, InventorySlots.Total).ToArray();
        slots[14] = new(389014000, 1, 2, 123, 0);
        var hp = new PotionDefinition(490014, 389014000, 1, 720, 2000, 1, 99, 90);
        var change = PotionRules.Propose(hp, slots, 100, 50, 1000, 1000, 80)!;
        test("potion heals and consumes exactly one without mutating input", change.Hp == 820 && change.Mp == 50 && change.Slots[14].Count == 1 && slots[14].Count == 2);
        test("potion clamps to max", PotionRules.Propose(hp, slots, 900, 50, 1000, 1000, 80)!.Hp == 1000);
        test("full/dead/level-invalid potion use rejected", PotionRules.Propose(hp, slots, 1000, 50, 1000, 1000, 80) is null && PotionRules.Propose(hp, slots, 0, 50, 1000, 1000, 80) is null && PotionRules.Propose(hp, slots, 100, 50, 1000, 1000, 100) is null);
        test("missing required item rejected", PotionRules.Propose(hp with { Item = 123 }, slots, 100, 50, 1000, 1000, 80) is null);
        slots[14] = slots[14] with { Count = 1 };
        test("last potion clears serial/count/durability", PotionRules.Propose(hp, slots, 100, 50, 1000, 1000, 80)!.Slots[14] == ItemSlotData.Empty);
        var mp = hp with { Resource = 2, Amount = 1920 };
        test("MP potion affects only MP", PotionRules.Propose(mp, slots, 100, 0, 5000, 5000, 80) is { Hp: 100, Mp: 1920 });
        var bytes = Convert.FromHexString("03247A0700E903E903000000000000000000000000");
        test("observed 21-byte use request parses", MagicPackets.TryRead(Packet.FromReceived(WizOpcode.MagicProcess, bytes), out var request) && request.Skill == 490020 && request.Caster == 1001);
        test("truncated request rejected", !MagicPackets.TryRead(Packet.FromReceived(WizOpcode.MagicProcess, bytes[..20]), out _));
        test("original 23-byte request accepted", MagicPackets.TryRead(Packet.FromReceived(WizOpcode.MagicProcess, bytes.Concat(new byte[2]).ToArray()), out _));
        test("magic success packet original 7-short layout", PotionRules.Magic(request, 1001, true).Length == 23 && PotionRules.Magic(request, 1001, true).Payload[11] == 1);
        test("stack change golden", Convert.ToHexString(PotionRules.Stack(389014000, 0, slots[14]).Payload) == "01000100F0E12F1701000000000100");
        test("target HP original layout including 32-bit NPC HP", Convert.ToHexString(TargetHealthPackets.Reply(10259, 1, 1000000, 1000000).Payload) == "13280140420F0040420F000000");
        test("target HP clamps corrupt values", TargetHealthPackets.Reply(1, 0, 0, -1).Payload[7] == 0);
    }

    public static async Task DatabaseAsync(DatabaseSettings settings, Action<string, bool> test)
    {
        await using var connection = new MySqlConnection(settings.BuildConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        var schema = await File.ReadAllTextAsync("sql/mysql/kn_online/0001_schema.sql");
        foreach (var table in new[] { "MAGIC", "MAGIC_TYPE3" })
        {
            command.CommandText = Regex.Match(schema, $@"CREATE TABLE `{table}`\([\s\S]*?\) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;").Value;
            await command.ExecuteNonQueryAsync();
        }
        command.CommandText = "ALTER TABLE USERDATA ADD Hp smallint NOT NULL DEFAULT 100, ADD Mp smallint NOT NULL DEFAULT 50";
        await command.ExecuteNonQueryAsync();
        var repository = new MariaDbCharacterRepository(settings);
        await repository.EnsurePotionDataAsync();
        await repository.EnsurePotionDataAsync();
        var hp = (await repository.LoadPotionAsync(490014))!;
        var mp = (await repository.LoadPotionAsync(490020))!;
        test("potion source HP/MP data and 100ms recast units", hp.Amount == 720 && mp.Amount == 1920 && hp.CooldownMs == 2000 && mp.Resource == 2);
        test("unsupported skill cannot become a consumable", await repository.LoadPotionAsync(210001) is null);
        var slots = Enumerable.Repeat(ItemSlotData.Empty, InventorySlots.Total).ToArray();
        slots[14] = new(hp.Item, 1, 2, 111, 0);
        slots[15] = new(mp.Item, 1, 2, 222, 0);
        var blob = ItemBlobs.Serialize(slots);
        var before = new InventorySnapshot(blob.Items, blob.Serials, 10000);
        command.CommandText = "INSERT INTO USERDATA(strUserID,Gold,strItem,strSerial,Hp,Mp) VALUES ('potion-test',10000,@items,@serials,100,50)";
        command.Parameters.AddWithValue("@items", blob.Items);
        command.Parameters.AddWithValue("@serials", blob.Serials);
        await command.ExecuteNonQueryAsync();
        var use = PotionRules.Propose(hp, slots, 100, 50, 5000, 5000, 80)!;
        blob = ItemBlobs.Serialize(use.Slots);
        var after = new InventorySnapshot(blob.Items, blob.Serials, 10000);
        var vitals = new PotionVitals(100, 50, use.Hp, use.Mp, 1, 60000);
        test("potion atomic consume/vitals/cooldown commit", await repository.TryUsePotionAsync("potion-test", before, after, hp.Item, 111, vitals) == InventorySaveResult.Saved);
        test("potion cooldown survives fresh repository/reconnect", await new MariaDbCharacterRepository(settings).TryUsePotionAsync("potion-test", after, before, hp.Item, 111, vitals with { ExpectedHp = 820 }) == InventorySaveResult.Cooldown);
        var mpUse = PotionRules.Propose(mp, use.Slots, 820, 50, 5000, 5000, 80)!;
        blob = ItemBlobs.Serialize(mpUse.Slots);
        var afterMp = new InventorySnapshot(blob.Items, blob.Serials, 10000);
        test("HP and MP cooldowns independent", await repository.TryUsePotionAsync("potion-test", after, afterMp, mp.Item, 222, new PotionVitals(820, 50, 820, 1970, 2, 60000)) == InventorySaveResult.Saved);
        command.CommandText = "SELECT Hp,Mp,strItem FROM USERDATA WHERE strUserID='potion-test'";
        await using (var row = await command.ExecuteReaderAsync())
        {
            await row.ReadAsync();
            test("potion reconnect shows both saved effects and counts", row.GetInt16(0) == 820 && row.GetInt16(1) == 1970 && ((byte[])row.GetValue(2)).SequenceEqual(afterMp.Items));
        }
        command.CommandText = "UPDATE KO_POTION_COOLDOWN SET AvailableAt='2000-01-01' WHERE strUserID='potion-test'; ALTER TABLE USERDATA ADD CONSTRAINT potion_failure CHECK (strUserID<>'potion-test' OR Hp=820)";
        await command.ExecuteNonQueryAsync();
        var threw = false;
        try { await repository.TryUsePotionAsync("potion-test", afterMp, before, hp.Item, 111, new PotionVitals(820,1970,999,1970,1,2000)); } catch (MySqlException) { threw = true; }
        test("potion SQL failure rolls back instead of granting free heal", threw);
        command.CommandText = "SELECT Hp,Mp,strItem FROM USERDATA WHERE strUserID='potion-test'";
        await using var final = await command.ExecuteReaderAsync();
        await final.ReadAsync();
        test("potion failure retains previous inventory and vitals", final.GetInt16(0) == 820 && final.GetInt16(1) == 1970 && ((byte[])final.GetValue(2)).SequenceEqual(afterMp.Items));
    }
}
