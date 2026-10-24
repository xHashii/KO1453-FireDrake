using KnightOnline.Database.Items;
using MySqlConnector;
namespace KnightOnline.Database;
public sealed record WarehouseSnapshot(byte[] Items,byte[] Serials,byte[] Times,int Gold,bool HasTimes);
internal sealed record WarehouseMutation(string Account,WarehouseSnapshot Expected,WarehouseSnapshot Proposed,(uint Item,ulong Serial)[] Protected);
public sealed partial class MariaDbCharacterRepository
{
    // The original account-creation path used X'00' for an empty warehouse.
    // Retain that exact raw snapshot for CAS; parse it as zero slots without
    // rewriting it on open. Arbitrary nonzero partial records remain invalid.
    private static bool ValidWarehouseBlob(byte[] data,int width) => data.Length%width==0 || (data.Length==1 && data[0]==0);
    private static async Task<bool> WarehouseHasTimes(MySqlCommand cmd)
    {
        cmd.CommandText="SELECT COUNT(*) FROM information_schema.columns WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='WAREHOUSE' AND COLUMN_NAME='WarehouseDataTime'";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync())==1;
    }
    public async Task<WarehouseSnapshot> LoadWarehouseAsync(string account)
    {
        await using var c=new MySqlConnection(_connectionString); await c.OpenAsync(); await using var cmd=c.CreateCommand();
        var times=await WarehouseHasTimes(cmd);
        cmd.CommandText="SELECT WarehouseData,strSerial,nMoney"+(times?",WarehouseDataTime":"")+" FROM WAREHOUSE WHERE strAccountID=@account";cmd.Parameters.AddWithValue("@account",account);
        await using var r=await cmd.ExecuteReaderAsync(); if(!await r.ReadAsync()) throw new InvalidDataException("Warehouse is missing.");
        var result=new WarehouseSnapshot(r.IsDBNull(0)?[]:(byte[])r[0],r.IsDBNull(1)?[]:(byte[])r[1],times&&!r.IsDBNull(3)?(byte[])r[3]:[],Convert.ToInt32(r[2]),times);
        if(await r.ReadAsync()) throw new InvalidDataException("Duplicate warehouse identity.");
        if(result.Gold is <0 or >ShopRules.CoinMax || !ValidWarehouseBlob(result.Items,8) || !ValidWarehouseBlob(result.Serials,8) || !ValidWarehouseBlob(result.Times,4)) throw new InvalidDataException("Misaligned warehouse blobs; refusing lossy normalization.");
        return result;
    }
    public Task<InventorySaveResult> SaveWarehouseAsync(string character,string account,InventorySnapshot expected,InventorySnapshot proposed,WarehouseSnapshot oldBank,WarehouseSnapshot newBank,(uint Item,ulong Serial)[] protections) =>
        SaveInventoryAsync(character,expected,proposed,0,0,null,warehouse:new(account,oldBank,newBank,protections));
    private static async Task<bool> LockAndSaveWarehouse(MySqlCommand cmd,WarehouseMutation mutation,InventorySnapshot inventory,InventorySnapshot proposed)
    {
        var old=mutation.Expected; var next=mutation.Proposed;
        if(next.Gold is <0 or >ShopRules.CoinMax || old.HasTimes!=next.HasTimes) return false;
        // The runtime does not yet execute timed inventory effects. Never move an
        // item whose optional persisted expiration metadata would be lost.
        cmd.CommandText="SELECT COUNT(*) FROM information_schema.columns WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='USERDATA' AND COLUMN_NAME='strItemTime'";
        if(Convert.ToInt32(await cmd.ExecuteScalarAsync())!=0)
        {
            cmd.CommandText="SELECT strItemTime FROM USERDATA WHERE strUserID=@id";
            var timeValue=await cmd.ExecuteScalarAsync(); var times=timeValue is byte[] bytes?bytes:[];
            for(var i=0;i<times.Length/4;i++)
            {
                var offset=i*8;
                if(offset+8>inventory.Items.Length || offset+8>proposed.Items.Length) continue;
                if(!inventory.Items.AsSpan(offset,8).SequenceEqual(proposed.Items.AsSpan(offset,8)) && System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(times.AsSpan(i*4))!=0) return false;
            }
        }
        cmd.Parameters.AddWithValue("@account",mutation.Account);
        cmd.CommandText="SELECT strAccountID,strCharID1,strCharID2,strCharID3 FROM ACCOUNT_CHAR WHERE strAccountID=@account FOR UPDATE";
        await using(var r=await cmd.ExecuteReaderAsync())
        {
            if(!await r.ReadAsync()) return false;
            var id=Convert.ToString(cmd.Parameters["@id"].Value)!;
            if(!Enumerable.Range(1,3).Any(i=>!r.IsDBNull(i)&&string.Equals(Convert.ToString(r[i])!.TrimEnd(),id,StringComparison.OrdinalIgnoreCase)) || await r.ReadAsync()) return false;
        }
        cmd.CommandText="SELECT strAccountID FROM WAREHOUSE WHERE strAccountID=@account FOR UPDATE";
        await using(var r=await cmd.ExecuteReaderAsync()) if(!await r.ReadAsync() || await r.ReadAsync()) return false;
        if(await WarehouseHasTimes(cmd)!=old.HasTimes) return false;
        // Check both the source and any occupied destination in the same transaction.
        foreach(var protection in mutation.Protected.Where(p=>p.Item!=0).Distinct())
        {
            cmd.Parameters.AddWithValue("@pid",protection.Item); cmd.Parameters.AddWithValue("@ps",unchecked((long)protection.Serial));
            cmd.CommandText="SELECT EXISTS(SELECT 1 FROM SEALED_ITEMS WHERE nItemID=@pid AND (@ps=0 OR nItemSerial=@ps)) OR EXISTS(SELECT 1 FROM USER_RENTAL_ITEM WHERE nItemIndex=@pid AND (@ps=0 OR nSerialNumber=@ps)) OR EXISTS(SELECT 1 FROM RENTAL_ITEM WHERE nItemIndex=@pid AND (@ps=0 OR nSerialNumber=@ps))";
            var blocked=Convert.ToInt32(await cmd.ExecuteScalarAsync())!=0;
            cmd.Parameters.RemoveAt("@pid");cmd.Parameters.RemoveAt("@ps"); if(blocked) return false;
        }
        cmd.Parameters.AddWithValue("@wi",next.Items);cmd.Parameters.AddWithValue("@ws",next.Serials);cmd.Parameters.AddWithValue("@wm",next.Gold);
        cmd.Parameters.AddWithValue("@oi",old.Items);cmd.Parameters.AddWithValue("@os",old.Serials);cmd.Parameters.AddWithValue("@om",old.Gold);
        if(old.HasTimes) {cmd.Parameters.AddWithValue("@wt",next.Times);cmd.Parameters.AddWithValue("@ot",old.Times);}
        cmd.CommandText="UPDATE WAREHOUSE SET WarehouseData=@wi,strSerial=@ws,nMoney=@wm"+(old.HasTimes?",WarehouseDataTime=@wt":"")+" WHERE strAccountID=@account AND nMoney=@om AND COALESCE(WarehouseData,X'')=@oi AND COALESCE(strSerial,X'')=@os"+(old.HasTimes?" AND COALESCE(WarehouseDataTime,X'')=@ot":"");
        return await cmd.ExecuteNonQueryAsync()==1;
    }
    public static WarehouseSnapshot WarehouseProposal(WarehouseSnapshot old,ItemSlotData[] slots,int gold)
    {
        var blobs=ItemBlobs.Serialize(slots);
        static byte[] Tail(byte[] data,byte[] previous) => previous.Length>data.Length?data.Concat(previous.Skip(data.Length)).ToArray():data;
        return new(Tail(blobs.Items,old.Items),Tail(blobs.Serials,old.Serials),old.HasTimes?Tail(blobs.Expirations,old.Times):old.Times,gold,old.HasTimes);
    }
}
