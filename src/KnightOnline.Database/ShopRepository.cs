using KnightOnline.Database.Items;
using MySqlConnector;

namespace KnightOnline.Database;

public sealed record InventorySnapshot(byte[] Items, byte[] Serials, int Gold);

public sealed partial class MariaDbCharacterRepository
{
    public async Task<ShopItem?> LoadShopItemAsync(uint id)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Num,BuyPrice,SellPrice,SellingGroup,Race,Countable,Weight,Duration FROM ITEM WHERE Num=@id";
        command.Parameters.AddWithValue("@id", id);
        await using var row = await command.ExecuteReaderAsync().ConfigureAwait(false);
        return await row.ReadAsync().ConfigureAwait(false)
            ? new ShopItem(Convert.ToUInt32(row.GetValue(0)), Convert.ToInt32(row.GetValue(1)), Convert.ToInt32(row.GetValue(2)),
                Convert.ToByte(row.GetValue(3)), Convert.ToByte(row.GetValue(4)), Convert.ToByte(row.GetValue(5)),
                Convert.ToInt16(row.GetValue(6)), Convert.ToInt16(row.GetValue(7)))
            : null;
    }

    /// <summary>A row-locked transaction commits coins AND both parallel blobs, or neither.
    /// Compare-and-swap rejects stale sessions/admin edits. A response lost after commit is
    /// never retried by the server: reconnect reads the committed state. The legacy protocol
    /// has no transaction nonce; two valid purchases are two separately charged purchases.</summary>
    public async Task<bool> TrySaveInventoryAsync(string charId, InventorySnapshot expected, InventorySnapshot proposed,
        uint protectedItem = 0, ulong protectedSerial = 0) =>
        await SaveInventoryAsync(charId, expected, proposed, protectedItem, protectedSerial, null).ConfigureAwait(false) == InventorySaveResult.Saved;

    public Task<InventorySaveResult> TryUsePotionAsync(string charId, InventorySnapshot expected, InventorySnapshot proposed,
        uint item, ulong serial, PotionVitals vitals) => SaveInventoryAsync(charId, expected, proposed, item, serial, vitals);

    private async Task<InventorySaveResult> SaveInventoryAsync(string charId, InventorySnapshot expected, InventorySnapshot proposed,
        uint protectedItem, ulong protectedSerial, PotionVitals? potion)
    {
        if (proposed.Gold < 0 || proposed.Gold > ShopRules.CoinMax) return InventorySaveResult.StaleOrProtected;
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("@id", charId);
        // The shipped USERDATA DDL has no unique key. Reject ambiguous identity
        // instead of committing multiple rows, or only one of several duplicates.
        command.CommandText = "SELECT strUserID FROM USERDATA WHERE strUserID=@id FOR UPDATE";
        await using (var rows = await command.ExecuteReaderAsync().ConfigureAwait(false))
        {
            if (!await rows.ReadAsync().ConfigureAwait(false) || await rows.ReadAsync().ConfigureAwait(false)) return InventorySaveResult.StaleOrProtected;
        }
        if (potion is not null)
        {
            if (potion.Hp <= 0 || potion.Mp < 0 || potion.Resource is not (1 or 2) || potion.CooldownMs < 500)
                return InventorySaveResult.StaleOrProtected;
            command.Parameters.AddWithValue("@resource", potion.Resource);
            command.CommandText = "INSERT INTO KO_POTION_COOLDOWN VALUES (@id,@resource,UTC_TIMESTAMP(6)) ON DUPLICATE KEY UPDATE strUserID=VALUES(strUserID)";
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            command.CommandText = "SELECT AvailableAt>UTC_TIMESTAMP(6) FROM KO_POTION_COOLDOWN WHERE strUserID=@id AND Resource=@resource FOR UPDATE";
            if (Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false)) != 0) return InventorySaveResult.Cooldown;
            command.Parameters.AddWithValue("@hp", potion.Hp);
            command.Parameters.AddWithValue("@mp", potion.Mp);
            command.Parameters.AddWithValue("@oldHp", potion.ExpectedHp);
            command.Parameters.AddWithValue("@oldMp", potion.ExpectedMp);
        }
        command.CommandText = "UPDATE USERDATA SET strItem=@items,strSerial=@serials,Gold=@gold " +
            (potion is null ? "" : ",Hp=@hp,Mp=@mp ") +
            "WHERE strUserID=@id AND Gold=@oldGold AND COALESCE(strItem,X'')=@oldItems AND COALESCE(strSerial,X'')=@oldSerials " +
            "AND (@protectedItem=0 OR (" +
            "NOT EXISTS (SELECT 1 FROM SEALED_ITEMS WHERE nItemID=@protectedItem AND ((@serial<>0 AND nItemSerial=@serial) OR (@serial=0 AND strUserID=@id))) " +
            "AND NOT EXISTS (SELECT 1 FROM USER_RENTAL_ITEM WHERE nItemIndex=@protectedItem AND ((@serial<>0 AND nSerialNumber=@serial) OR (@serial=0 AND strUserID=@id))) " +
            "AND NOT EXISTS (SELECT 1 FROM RENTAL_ITEM WHERE nItemIndex=@protectedItem AND ((@serial<>0 AND nSerialNumber=@serial) OR (@serial=0 AND strBorrowerCharID=@id)))))";
        if (potion is not null) command.CommandText += " AND Hp=@oldHp AND Mp=@oldMp";
        command.Parameters.AddWithValue("@oldGold", expected.Gold);
        command.Parameters.AddWithValue("@oldItems", expected.Items);
        command.Parameters.AddWithValue("@oldSerials", expected.Serials);
        command.Parameters.AddWithValue("@gold", proposed.Gold);
        command.Parameters.AddWithValue("@items", proposed.Items);
        command.Parameters.AddWithValue("@serials", proposed.Serials);
        command.Parameters.AddWithValue("@protectedItem", protectedItem);
        command.Parameters.AddWithValue("@serial", unchecked((long)protectedSerial));
        if (await command.ExecuteNonQueryAsync().ConfigureAwait(false) != 1) return InventorySaveResult.StaleOrProtected;
        if (potion is not null)
        {
            command.Parameters.AddWithValue("@micros", (long)potion.CooldownMs * 1000);
            command.CommandText = "UPDATE KO_POTION_COOLDOWN SET AvailableAt=TIMESTAMPADD(MICROSECOND,@micros,UTC_TIMESTAMP(6)) WHERE strUserID=@id AND Resource=@resource";
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        await transaction.CommitAsync().ConfigureAwait(false);
        return InventorySaveResult.Saved;
    }
}
