using KnightOnline.Database;
using KnightOnline.Database.Items;
using MySqlConnector;
namespace KnightOnline.Verification;
internal static class ItemSealVerification
{
    public static void Rules(Action<string,bool> test)
    {
        var salt = Enumerable.Range(0,16).Select(i=>(byte)i).ToArray();
        var hash = SealCode.Hash("87654321",salt);
        test("seal code salted hash verifies only matching code",SealCode.Verify("87654321",salt,hash)&&!SealCode.Verify("87654320",salt,hash)&&!SealCode.Verify("87654321",new byte[16],hash));
        test("seal code policy rejects public legacy default and weak/malformed codes",!SealCode.Valid("12345678")&&!SealCode.Valid("11111111")&&!SealCode.Valid("1234567")&&!SealCode.Valid("1234567a")&&SealCode.Valid("87654321"));
        var req = new ItemSealRequest(1,0x12345678,27,"87654321");
        test("seal DB success is one with exact item/position echo",Convert.ToHexString(ItemSealPackets.Result(req,1).Payload)=="080101785634121B");
        test("seal failure never echoes code",Convert.ToHexString(ItemSealPackets.Result(req,4).Payload)=="080104785634121B"&&!req.ToString().Contains("87654321"));
        var plain = new ItemSlotData(100,99,1,123,0);
        test("seal flags do not corrupt legacy parallel blobs",ItemBlobs.Serialize([plain]).Items.SequenceEqual(ItemBlobs.Serialize([plain with {Flag=4}]).Items)&&ItemBlobs.Serialize([plain]).Serials.SequenceEqual(ItemBlobs.Serialize([plain with {Flag=4}]).Serials));
        var bank=Enumerable.Repeat(ItemSlotData.Empty,192).ToArray();bank[0]=plain with {Flag=4};
        test("warehouse protection flag has native field offset",WarehouseRules.Open(0,bank).Payload[14]==4);
    }
    public static async Task DatabaseAsync(DatabaseSettings settings,Action<string,bool> test)
    {
        var repo=new MariaDbCharacterRepository(settings);await repo.EnsureItemSealSchemaAsync();await repo.EnsureItemSealSchemaAsync();
        await using var c=new MySqlConnection(settings.BuildConnectionString());await c.OpenAsync();await using var cmd=c.CreateCommand();
        cmd.CommandText="CREATE TABLE TB_USER(strAccountID varchar(21)); INSERT INTO TB_USER VALUES('seal-owner'),('seal-other'); ALTER TABLE SEALED_ITEMS ADD strAccountID varchar(21),ADD bSealType tinyint unsigned; DELETE FROM SEALED_ITEMS; INSERT INTO ACCOUNT_CHAR VALUES('seal-owner','seal-char','',''),('seal-other','other-char','','')";
        await cmd.ExecuteNonQueryAsync();
        var slots=Enumerable.Repeat(ItemSlotData.Empty,75).ToArray();slots[14]=new(389014000,1,1,0x123456789abc,0);slots[74]=new(321,-23,42,99999,0);
        var blobs=ItemBlobs.Serialize(slots);var snapshot=new InventorySnapshot(blobs.Items,blobs.Serials,3000000);
        cmd.CommandText="INSERT INTO USERDATA(strUserID,Gold,strItem,strSerial) VALUES('seal-char',3000000,@i,@s)";cmd.Parameters.AddWithValue("@i",blobs.Items);cmd.Parameters.AddWithValue("@s",blobs.Serials);await cmd.ExecuteNonQueryAsync();cmd.Parameters.Clear();
        var seal=new ItemSealRequest(1,389014000,0,"87654321");var unseal=new ItemSealRequest(2,389014000,0,"87654321");
        test("seal requires explicit provisioned code; no legacy default fallback",(await repo.SealItemAsync("seal-owner","seal-char",snapshot,seal)).Code==4);
        await repo.SetItemSealCodeAsync("seal-owner","87654321");await repo.SetItemSealCodeAsync("seal-other","87654321");
        cmd.CommandText="SELECT HEX(CodeHash) FROM KO_ITEM_SEAL_SECURITY WHERE strAccountID='seal-owner'";
        test("DB seal code never stored as plaintext",Convert.ToString(await cmd.ExecuteScalarAsync())!.Length==64&&!Convert.ToString(await cmd.ExecuteScalarAsync())!.Contains("87654321"));
        test("seal rejects other account's character",(await repo.SealItemAsync("seal-other","seal-char",snapshot,seal)).Code==2);
        test("seal stale snapshot rejected",(await repo.SealItemAsync("seal-owner","seal-char",snapshot with {Gold=3000001},seal)).Code==2);
        test("seal wrong item/slot rejected",(await repo.SealItemAsync("seal-owner","seal-char",snapshot,new(1,123,0,"87654321"))).Code==2&&(await repo.SealItemAsync("seal-owner","seal-char",snapshot,new(1,389014000,1,"87654321"))).Code==2);
        for(var i=0;i<5;i++) test("wrong seal code rejected attempt "+i,(await repo.SealItemAsync("seal-owner","seal-char",snapshot,new(1,389014000,0,"87654320"))).Code==4);
        test("seal code retry lock survives repository recreation",(await new MariaDbCharacterRepository(settings).SealItemAsync("seal-owner","seal-char",snapshot,seal)).Code==7);
        cmd.CommandText="UPDATE KO_ITEM_SEAL_SECURITY SET LockedUntil=TIMESTAMPADD(SECOND,-1,UTC_TIMESTAMP(6)) WHERE strAccountID='seal-owner'";await cmd.ExecuteNonQueryAsync();
        var first=await repo.SealItemAsync("seal-owner","seal-char",snapshot,seal);
        test("seal commits protection and exactly one million fee",first.Code==1&&first.Gold==2000000);
        var sealedSnapshot=snapshot with {Gold=2000000};
        test("seal replay cannot charge twice",(await repo.SealItemAsync("seal-owner","seal-char",sealedSnapshot,seal)).Code==2);
        var restored=await new MariaDbCharacterRepository(settings).ApplyItemSealFlagsAsync("seal-owner",ItemBlobs.Parse(blobs.Items,blobs.Serials,[],75));
        test("sealed flag survives reconnect without changing serial/item/tail",restored[14]==(slots[14] with {Flag=4})&&restored[74]==slots[74]);
        test("sealed stack sale/consumption blocked transactionally",!await repo.TrySaveInventoryAsync("seal-char",sealedSnapshot,sealedSnapshot with {Gold=2000001},389014000,slots[14].Serial));
        var second=await repo.SealItemAsync("seal-owner","seal-char",sealedSnapshot,unseal);
        test("unseal authenticates and removes protection without another fee",second.Code==1&&second.Gold==2000000&&(await repo.ApplyItemSealFlagsAsync("seal-owner",restored))[14].Flag==0);
        test("unseal replay refused",(await repo.SealItemAsync("seal-owner","seal-char",sealedSnapshot,unseal)).Code==2);
        var racing=await Task.WhenAll(repo.SealItemAsync("seal-owner","seal-char",sealedSnapshot,seal),repo.SealItemAsync("seal-owner","seal-char",sealedSnapshot,seal));
        test("concurrent seals have one winner and one fee",racing.Count(r=>r.Code==1)==1);
        snapshot=sealedSnapshot with {Gold=1000000};
        test("unseal after concurrent seal succeeds",(await repo.SealItemAsync("seal-owner","seal-char",snapshot,unseal)).Code==1);
        cmd.CommandText="ALTER TABLE USERDATA ADD CONSTRAINT seal_rollback_test CHECK(strUserID<>'seal-char' OR Gold>=1000000)";await cmd.ExecuteNonQueryAsync();
        var threw=false;try{await repo.SealItemAsync("seal-owner","seal-char",snapshot,seal);}catch(MySqlException){threw=true;}
        cmd.CommandText="SELECT (SELECT COUNT(*) FROM SEALED_ITEMS WHERE strUserID='seal-char'),Gold,HEX(strItem),HEX(strSerial) FROM USERDATA WHERE strUserID='seal-char'";
        await using(var r=await cmd.ExecuteReaderAsync()){await r.ReadAsync();test("seal SQL failure rolls back protection/fee and preserves blobs",threw&&Convert.ToInt32(r[0])==0&&Convert.ToInt32(r[1])==1000000&&Convert.ToString(r[2])==Convert.ToHexString(blobs.Items)&&Convert.ToString(r[3])==Convert.ToHexString(blobs.Serials));}
        cmd.CommandText="ALTER TABLE USERDATA DROP CONSTRAINT seal_rollback_test; INSERT INTO ACCOUNT_CHAR VALUES('seal-other','seal-char','','')";await cmd.ExecuteNonQueryAsync();
        test("ambiguous second character owner refused",(await repo.SealItemAsync("seal-owner","seal-char",snapshot,seal)).Code==2);
    }
}
