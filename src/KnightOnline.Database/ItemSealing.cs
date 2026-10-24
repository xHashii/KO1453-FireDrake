using System.Security.Cryptography;
using KnightOnline.Database.Items;
using MySqlConnector;
namespace KnightOnline.Database;

public sealed record ItemSealResult(byte Code, int Gold);
public sealed partial class MariaDbCharacterRepository
{
    public const int ItemSealFee = 1000000;
    public async Task EnsureItemSealSchemaAsync()
    {
        await using var c = new MySqlConnection(_connectionString); await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        using var stream = typeof(MariaDbCharacterRepository).Assembly.GetManifestResourceStream("KnightOnline.Database.ItemSealSchema.sql")!;
        using var reader = new StreamReader(stream); cmd.CommandText = await reader.ReadToEndAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Operator-only provisioning using local DB credentials; not a game RPC.
    /// Does not import the unsafe legacy shared code or modify existing protections.</summary>
    public async Task SetItemSealCodeAsync(string account, string code)
    {
        if (!SealCode.Valid(code)) throw new ArgumentException("Use eight digits, not a repeated digit or the legacy default.");
        await using var c = new MySqlConnection(_connectionString); await c.OpenAsync();
        await using var tx = await c.BeginTransactionAsync(); await using var cmd = c.CreateCommand(); cmd.Transaction = tx;
        cmd.Parameters.AddWithValue("@account", account);
        cmd.CommandText = "SELECT strAccountID FROM TB_USER WHERE strAccountID=@account FOR UPDATE";
        await using (var r = await cmd.ExecuteReaderAsync())
            if (!await r.ReadAsync() || await r.ReadAsync()) throw new InvalidDataException("Missing or ambiguous account.");
        var salt = RandomNumberGenerator.GetBytes(16);
        cmd.Parameters.AddWithValue("@salt", salt); cmd.Parameters.AddWithValue("@hash", SealCode.Hash(code, salt));
        cmd.CommandText = "INSERT INTO KO_ITEM_SEAL_SECURITY(strAccountID,Salt,CodeHash) VALUES(@account,@salt,@hash) ON DUPLICATE KEY UPDATE Salt=@salt,CodeHash=@hash,FailedAttempts=0,LockedUntil=NULL";
        await cmd.ExecuteNonQueryAsync(); await tx.CommitAsync();
    }

    public async Task<ItemSlotData[]> ApplyItemSealFlagsAsync(string account, ItemSlotData[] slots)
    {
        await using var c = new MySqlConnection(_connectionString); await c.OpenAsync(); await using var cmd = c.CreateCommand();
        // Resolve globally by serial, not by claimed owner: foreign/ambiguous protection
        // must never become an ordinary item merely because a legacy blob moved it.
        var protectedItems = new Dictionary<ulong, (uint Item, byte Flag)>();
        var serials = slots.Where(s => !s.IsEmpty && s.Serial != 0).Select(s => s.Serial).Distinct().ToArray();
        if (serials.Length == 0) return slots;
        for (var i = 0; i < serials.Length; i++) cmd.Parameters.AddWithValue("@s" + i, unchecked((long)serials[i]));
        cmd.CommandText = "SELECT nItemSerial,nItemID,bSealType,strAccountID FROM SEALED_ITEMS WHERE nItemSerial IN (" + string.Join(",", Enumerable.Range(0, serials.Length).Select(i => "@s" + i)) + ")";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var serial = unchecked((ulong)Convert.ToInt64(r[0])); var item = Convert.ToUInt32(r[1]); var type = Convert.ToByte(r[2]);
            if (!string.Equals(Convert.ToString(r[3])!.TrimEnd(), account, StringComparison.OrdinalIgnoreCase) || type is not (1 or 3) || !protectedItems.TryAdd(serial, (item, type == 1 ? (byte)4 : (byte)8)))
                throw new InvalidDataException("Ambiguous or foreign item protection; refusing unsafe inventory.");
        }
        return slots.Select(s => protectedItems.TryGetValue(s.Serial, out var p)
            ? s.ItemId == p.Item ? s with { Flag = p.Flag } : throw new InvalidDataException("Protected serial/item mismatch.")
            : s with { Flag = 0 }).ToArray();
    }

    /// <summary>All expected refusals roll back. Only failed-code counters deliberately
    /// commit on rejection. Success=1 matches USER_ITEM_SEAL/ReqSealItem, NOT the
    /// misleading SealErrorNone=0 preflight constant. No automatic retry after exceptions.</summary>
    public async Task<ItemSealResult> SealItemAsync(string account, string character, InventorySnapshot expected, ItemSealRequest request)
    {
        ItemSealResult Fail(byte code = 2) => new(code, expected.Gold);
        if (request.Action is not (1 or 2) || request.Position >= 28 || !SealCode.Valid(request.Password)) return Fail(4);
        if (expected.Items.Length % 8 != 0 || expected.Serials.Length % 8 != 0 || expected.Gold < 0) return Fail();
        var slots = ItemBlobs.Parse(expected.Items, expected.Serials, [], Math.Max(73, expected.Items.Length / 8));
        var source = slots[14 + request.Position];
        if (source.IsEmpty || source.ItemId != request.Item || source.Serial == 0 || slots.Count(s => !s.IsEmpty && s.Serial == source.Serial) != 1) return Fail();
        await using var c = new MySqlConnection(_connectionString); await c.OpenAsync();
        await using var tx = await c.BeginTransactionAsync(); await using var cmd = c.CreateCommand(); cmd.Transaction = tx;
        cmd.Parameters.AddWithValue("@account", account); cmd.Parameters.AddWithValue("@id", character);
        cmd.CommandText = "SELECT Salt,CodeHash,FailedAttempts,COALESCE(LockedUntil>UTC_TIMESTAMP(6),0) FROM KO_ITEM_SEAL_SECURITY WHERE strAccountID=@account FOR UPDATE";
        byte[] salt, hash; int failures;
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            if (!await r.ReadAsync()) return Fail(4);
            salt = (byte[])r[0]; hash = (byte[])r[1]; failures = Convert.ToInt32(r[2]);
            if (Convert.ToInt32(r[3]) != 0) return Fail(7);
        }
        if (!SealCode.Verify(request.Password, salt, hash))
        {
            cmd.Parameters.AddWithValue("@failures", failures + 1 >= 5 ? 0 : failures + 1);
            cmd.CommandText = "UPDATE KO_ITEM_SEAL_SECURITY SET FailedAttempts=@failures,LockedUntil=" + (failures + 1 >= 5 ? "TIMESTAMPADD(MINUTE,5,UTC_TIMESTAMP(6))" : "NULL") + " WHERE strAccountID=@account";
            await cmd.ExecuteNonQueryAsync(); await tx.CommitAsync(); return Fail(4);
        }
        cmd.CommandText = "SELECT Gold,strItem,strSerial FROM USERDATA WHERE strUserID=@id FOR UPDATE";
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            if (!await r.ReadAsync() || Convert.ToInt32(r[0]) != expected.Gold || !((r.IsDBNull(1) ? [] : (byte[])r[1]).AsSpan().SequenceEqual(expected.Items)) || !((r.IsDBNull(2) ? [] : (byte[])r[2]).AsSpan().SequenceEqual(expected.Serials)) || await r.ReadAsync()) return Fail();
        }
        // Lock the whole account mapping and reject any second owner, including another
        // account pointing at this character. Never authorize from the client name alone.
        cmd.CommandText = "SELECT strAccountID,strCharID1,strCharID2,strCharID3 FROM ACCOUNT_CHAR WHERE strAccountID=@account OR strCharID1=@id OR strCharID2=@id OR strCharID3=@id FOR UPDATE";
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            if (!await r.ReadAsync() || !string.Equals(Convert.ToString(r[0])!.TrimEnd(), account, StringComparison.OrdinalIgnoreCase) || Enumerable.Range(1, 3).Count(i => !r.IsDBNull(i) && string.Equals(Convert.ToString(r[i])!.TrimEnd(), character, StringComparison.OrdinalIgnoreCase)) != 1 || await r.ReadAsync()) return Fail();
        }
        cmd.Parameters.AddWithValue("@serial", unchecked((long)source.Serial)); cmd.Parameters.AddWithValue("@item", source.ItemId);
        cmd.CommandText = "SELECT COUNT(*) FROM ITEM WHERE Num=@item";
        if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) != 1) return Fail();
        cmd.CommandText = "INSERT INTO KO_ITEM_SEAL_LOCK(nItemSerial) VALUES(@serial) ON DUPLICATE KEY UPDATE nItemSerial=VALUES(nItemSerial)";
        await cmd.ExecuteNonQueryAsync();
        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM USER_RENTAL_ITEM WHERE nSerialNumber=@serial) OR EXISTS(SELECT 1 FROM RENTAL_ITEM WHERE nSerialNumber=@serial)";
        if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) != 0) return Fail();
        cmd.CommandText = "SELECT strAccountID,strUserID,nItemID,bSealType FROM SEALED_ITEMS WHERE nItemSerial=@serial FOR UPDATE";
        var present = false;
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            present = await r.ReadAsync();
            if (present && (request.Action == 1 || !string.Equals(Convert.ToString(r[0])!.TrimEnd(), account, StringComparison.OrdinalIgnoreCase) || !string.Equals(Convert.ToString(r[1])!.TrimEnd(), character, StringComparison.OrdinalIgnoreCase) || Convert.ToUInt32(r[2]) != source.ItemId || Convert.ToByte(r[3]) != 1 || await r.ReadAsync())) return Fail();
        }
        if (request.Action == 2 && !present) return Fail();
        var gold = expected.Gold;
        if (request.Action == 1)
        {
            if (gold < ItemSealFee) return Fail(3);
            gold -= ItemSealFee;
            cmd.CommandText = "INSERT INTO SEALED_ITEMS(strAccountID,strUserID,nItemSerial,nItemID,bSealType) VALUES(@account,@id,@serial,@item,1)";
        }
        else cmd.CommandText = "DELETE FROM SEALED_ITEMS WHERE strAccountID=@account AND strUserID=@id AND nItemSerial=@serial AND nItemID=@item AND bSealType=1";
        if (await cmd.ExecuteNonQueryAsync() != 1) return Fail();
        cmd.Parameters.AddWithValue("@gold", gold);
        cmd.CommandText = "UPDATE USERDATA SET Gold=@gold WHERE strUserID=@id";
        if (await cmd.ExecuteNonQueryAsync() != 1) return Fail();
        cmd.CommandText = "UPDATE KO_ITEM_SEAL_SECURITY SET FailedAttempts=0,LockedUntil=NULL WHERE strAccountID=@account";
        await cmd.ExecuteNonQueryAsync(); await tx.CommitAsync(); return new(1, gold);
    }
}
