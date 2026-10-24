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

    /// <summary>A single InnoDB statement commits coins AND both parallel blobs, or neither.
    /// Compare-and-swap rejects stale sessions/admin edits. A response lost after commit is
    /// never retried by the server: reconnect reads the committed state. The legacy protocol
    /// has no transaction nonce; two valid purchases are two separately charged purchases.</summary>
    public async Task<bool> TrySaveInventoryAsync(string charId, InventorySnapshot expected, InventorySnapshot proposed,
        uint protectedItem = 0, ulong protectedSerial = 0)
    {
        if (proposed.Gold < 0 || proposed.Gold > ShopRules.CoinMax) return false;
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE USERDATA SET strItem=@items,strSerial=@serials,Gold=@gold " +
            "WHERE strUserID=@id AND Gold=@oldGold AND COALESCE(strItem,X'')=@oldItems AND COALESCE(strSerial,X'')=@oldSerials " +
            "AND (@protectedItem=0 OR (" +
            "NOT EXISTS (SELECT 1 FROM SEALED_ITEMS WHERE nItemID=@protectedItem AND ((@serial<>0 AND nItemSerial=@serial) OR (@serial=0 AND strUserID=@id))) " +
            "AND NOT EXISTS (SELECT 1 FROM USER_RENTAL_ITEM WHERE nItemIndex=@protectedItem AND ((@serial<>0 AND nSerialNumber=@serial) OR (@serial=0 AND strUserID=@id))) " +
            "AND NOT EXISTS (SELECT 1 FROM RENTAL_ITEM WHERE nItemIndex=@protectedItem AND ((@serial<>0 AND nSerialNumber=@serial) OR (@serial=0 AND strBorrowerCharID=@id)))))";
        command.Parameters.AddWithValue("@id", charId);
        command.Parameters.AddWithValue("@oldGold", expected.Gold);
        command.Parameters.AddWithValue("@oldItems", expected.Items);
        command.Parameters.AddWithValue("@oldSerials", expected.Serials);
        command.Parameters.AddWithValue("@gold", proposed.Gold);
        command.Parameters.AddWithValue("@items", proposed.Items);
        command.Parameters.AddWithValue("@serials", proposed.Serials);
        command.Parameters.AddWithValue("@protectedItem", protectedItem);
        command.Parameters.AddWithValue("@serial", unchecked((long)protectedSerial));
        return await command.ExecuteNonQueryAsync().ConfigureAwait(false) == 1;
    }
}
