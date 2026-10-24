using KnightOnline.Database;
using KnightOnline.Database.Items;
using KnightOnline.Shared.Network;
using KnightOnline.Shared.Protocol;
using MySqlConnector;
namespace KnightOnline.Verification;
internal static class FoundationsVerification
{
    public static void Rules(Action<string,bool> test)
    {
        var items=Enumerable.Range(0,73).Select(i=>new ItemSlotData((uint)(1000+i),(short)(100+i),1,(ulong)i,0)).ToArray();
        var info=PlayerPackets.Info(new("A",2,80,12,206,1234,5678,-30,1,2,1,1,-90,0,21,items));
        test("player info matches original C++ clanless wire layout and ten-slot order",Convert.ToHexString(info)=="01410200000000000000000000FFFF500CCE00D2042E16E2FF010201010000000101000000A6FF0000FFFFEC030000680000F20300006E0000E9030000650000F4030000700000F5030000710000ED030000690000EE0300006A0000F00300006C000016040000920000130400008F000015");
        var filtered=PlayerPackets.Info(new("A",2,80,12,206,1234,5678,-30,1,2,1,1,-90,0,21,items),id=>id<1040);
        test("remote appearance suppresses unknown costume models without changing saved items",filtered.AsSpan(filtered.Length-15,14).ToArray().All(b=>b==0)&&items[46].ItemId==1046);
        var hub=new PlayerHub();var a=new List<Packet>();var b=new List<Packet>();var c=new List<Packet>();
        PlayerPresence P(ushort id,ushort zone,float x,List<Packet> sink,int version=0)=>new(id,zone,x,0,info,version,p=>sink.Add(p));
        hub.Publish(P(1,21,0,a));hub.Publish(P(2,21,10,b));hub.Publish(P(3,1,10,c));
        test("nearby players receive mutual IN without self or cross-zone ghosts",a.Count==1&&b.Count==1&&c.Count==0&&a[0].Payload[0]==1&&a[0].Payload[1]==0);
        a.Clear();b.Clear();hub.Publish(P(2,21,10,b));
        test("unchanged presence does not duplicate IN",a.Count==0&&b.Count==0);
        hub.Broadcast(1,new Packet(WizOpcode.Rotate).WriteUInt16(1).WriteInt16(-90));
        test("rotation only reaches visible peers",a.Count==0&&b.Count==1&&b[0].Opcode==WizOpcode.Rotate&&c.Count==0);
        a.Clear();b.Clear();hub.Publish(P(2,21,500,b));
        test("leaving range produces mutual OUT",a.Count==1&&b.Count==1&&a[0].Payload[0]==2&&b[0].Payload[0]==2);
        test("requested user info cannot reveal out-of-range or cross-zone users",hub.Requested(1,[2,3,999]).Single().Payload.SequenceEqual(new byte[]{0,0}));
        a.Clear();b.Clear();hub.Publish(P(2,21,10,b));a.Clear();b.Clear();hub.Publish(P(2,21,10,b,1));
        test("equipment revision refreshes peers OUT then IN without resetting self",a.Count==2&&a[0].Payload[0]==2&&a[1].Payload[0]==1&&b.Count==0);
        var requested=hub.Requested(1,[2,2,3]).Single();
        test("requested user record includes count, zero marker and ushort socket ID",requested.Payload[0]==1&&requested.Payload[2]==0&&requested.Payload[3]==2&&requested.Payload[4]==0);
        a.Clear();b.Clear();hub.Remove(2);hub.Remove(2);
        test("disconnect sends exactly one OUT",a.Count==1&&a[0].Payload[0]==2);
        hub.Publish(P(2,21,10,b));a.Clear();hub.Publish(P(2,1,10,b));
        test("zone changes remove old-zone player visibility",a.Count==1&&a[0].Payload[0]==2);
        for(ushort id=10;id<75;id++) hub.Publish(P(id,21,0,new List<Packet>()));
        var batches=hub.Requested(1,Enumerable.Range(10,65).Select(i=>(ushort)i));
        test("large player requests use bounded thirty-record batches",batches.Count==3&&batches.All(p=>p.Length<8192)&&batches.Sum(p=>(int)p.Payload[0])==65);
        var save=new Packet(WizOpcode.Skilldata).WriteByte(1).WriteUInt16(3).WriteUInt32(490014).WriteUInt32(1490020).WriteUInt32(uint.MaxValue);
        test("skill bar parses opaque shortcut IDs without executing them",SkillBarPackets.TryRead(save,out var action,out var values)&&action==1&&values.SequenceEqual(new uint[]{490014,1490020,uint.MaxValue}));
        test("skill bar native load vector",Convert.ToHexString(SkillBarPackets.Loaded([0x01020304]).Payload)=="02010004030201");
        test("skill bar rejects oversized counts",!SkillBarPackets.TryRead(new Packet(WizOpcode.Skilldata).WriteByte(1).WriteUInt16(65),out _,out _));
        test("skill bar rejects truncated payload",!SkillBarPackets.TryRead(new Packet(WizOpcode.Skilldata).WriteByte(1).WriteUInt16(1),out _,out _));
        test("skill bar rejects trailing load bytes",!SkillBarPackets.TryRead(new Packet(WizOpcode.Skilldata).WriteByte(2).WriteByte(0),out _,out _));
        test("skill bar accepts empty clear",SkillBarPackets.TryRead(new Packet(WizOpcode.Skilldata).WriteByte(1).WriteUInt16(0),out _,out var empty)&&empty.Length==0);
        var inv=Enumerable.Repeat(ItemSlotData.Empty,75).ToArray();inv[14]=new(389014000,1,10,12345,0);inv[74]=new(65537,-8687,5044,0,0);
        var bank=Enumerable.Repeat(ItemSlotData.Empty,192).ToArray();var item=new ShopItem(389014000,1,0,0,0,1,5,1);
        var request=new WarehouseRequest(2,10001,item.Id,0,0,0,3);
        WarehouseChange? Propose(WarehouseRequest r,long weight=50,int max=1000)=>WarehouseRules.Propose(r,item,inv,bank,100,100,weight,max);
        var deposit=Propose(request)!;
        test("warehouse partial deposit conserves count and identity",deposit.Inventory[14].Count==7&&deposit.Warehouse[0]==(inv[14] with {Count=3})&&inv[14].Count==10&&deposit.Inventory[74]==inv[74]);
        var withdrawal=WarehouseRules.Propose(request with {Action=3,Count=3},item,deposit.Inventory,deposit.Warehouse,100,100,35,1000)!;
        test("warehouse withdrawal restores stack and leaves bank empty",withdrawal.Inventory[14]==inv[14]&&withdrawal.Warehouse[0]==ItemSlotData.Empty);
        test("warehouse strict page and slot bounds",Propose(request with {Page=8}) is null&&Propose(request with {Source=28}) is null&&Propose(request with {Destination=24}) is null);
        test("warehouse zero and excessive counts rejected",Propose(request with {Count=0}) is null&&Propose(request with {Count=11}) is null);
        test("warehouse withdrawal enforces weight",WarehouseRules.Propose(request with {Action=3},item,deposit.Inventory,deposit.Warehouse,100,100,999,1000) is null);
        bank[0]=new(item.Id,1,9998,6789,0);
        test("warehouse stack overflow rejected",Propose(request) is null);bank[0]=ItemSlotData.Empty;
        inv[14]=inv[14] with {ExpirationUnixTime=123};test("warehouse timed items refused without dropping expiration",Propose(request) is null);inv[14]=inv[14] with {ExpirationUnixTime=0};
        test("warehouse coin transfer conserves total",Propose(request with {Item=900000000,Count=50}) is {Gold:50,Bank:150});
        test("warehouse coin underflow/overflow rejected",Propose(request with {Item=900000000,Count=101}) is null&&WarehouseRules.Propose(request with {Item=900000000,Count=1},null,inv,bank,100,ShopRules.CoinMax,0,1000) is null);
        test("warehouse open has exact 192 native item records",WarehouseRules.Open(100,bank).Length==6+192*17);
        test("warehouse source PK-zone opening fee",WarehouseRules.OpenFee(71)==10000&&WarehouseRules.OpenFee(72)==10000&&WarehouseRules.OpenFee(73)==10000&&WarehouseRules.OpenFee(21)==0);
        test("warehouse native transfer success vector",Convert.ToHexString(WarehouseRules.Result(2,true).Payload)=="0201");
        var wire=new Packet(WizOpcode.Warehouse).WriteByte(2).WriteUInt16(10001).WriteUInt32(item.Id).WriteByte(0).WriteByte(0).WriteByte(0).WriteUInt32(3);
        test("warehouse native request parses exact fields",WarehouseRules.TryRead(wire,out var decoded)&&decoded==request);
        test("warehouse malformed transfer rejected",!WarehouseRules.TryRead(new Packet(WizOpcode.Warehouse).WriteByte(2),out _));
    }
    public static async Task DatabaseAsync(DatabaseSettings settings,Action<string,bool> test)
    {
        var repo=new MariaDbCharacterRepository(settings);await repo.EnsureFoundationSchemaAsync();await repo.EnsureFoundationSchemaAsync();
        await using var c=new MySqlConnection(settings.BuildConnectionString());await c.OpenAsync();await using var cmd=c.CreateCommand();
        var inv=Enumerable.Repeat(ItemSlotData.Empty,75).ToArray();inv[14]=new(389014000,1,10,12345,0);inv[74]=new(65537,-8687,5044,0,0);
        var blobs=ItemBlobs.Serialize(inv);var snapshot=new InventorySnapshot(blobs.Items,blobs.Serials,1000);
        cmd.CommandText="INSERT INTO USERDATA(strUserID,Gold,strItem,strSerial,Hp,Mp,Level,Exp) VALUES('foundation-test',1000,@i,@s,77,33,80,123); INSERT INTO ACCOUNT_CHAR VALUES('foundation-account','foundation-test','',''); DROP TABLE IF EXISTS WAREHOUSE; CREATE TABLE WAREHOUSE(strAccountID varchar(21),nMoney int,dwTime int,WarehouseData blob,strSerial blob) ENGINE=InnoDB; INSERT INTO WAREHOUSE VALUES('foundation-account',1000,0,X'',X'')";
        cmd.Parameters.AddWithValue("@i",snapshot.Items);cmd.Parameters.AddWithValue("@s",snapshot.Serials);await cmd.ExecuteNonQueryAsync();cmd.Parameters.Clear();
        await repo.SaveSkillBarAsync("foundation-test",[490014,1490020,uint.MaxValue]);
        test("skill bar reconnect round trip",(await new MariaDbCharacterRepository(settings).LoadSkillBarAsync("foundation-test")).SequenceEqual(new uint[]{490014,1490020,uint.MaxValue}));
        await repo.SaveSkillBarAsync("foundation-test",[]);test("empty skill bar persists",(await repo.LoadSkillBarAsync("foundation-test")).Length==0);
        var bank=await repo.LoadWarehouseAsync("foundation-account");var stored=ItemBlobs.Parse(bank.Items,bank.Serials,bank.Times,192);
        var nextBank=MariaDbCharacterRepository.WarehouseProposal(bank,stored,1100);var next=snapshot with {Gold=900};
        test("bank and character coins commit atomically",await repo.SaveWarehouseAsync("foundation-test","foundation-account",snapshot,next,bank,nextBank,[])==InventorySaveResult.Saved);
        test("stale bank snapshot cannot duplicate transfer",await repo.SaveWarehouseAsync("foundation-test","foundation-account",snapshot,next,bank,nextBank,[])!=InventorySaveResult.Saved);
        snapshot=next;bank=await repo.LoadWarehouseAsync("foundation-account");nextBank=bank with {Gold=1110};next=snapshot with {Gold=890};
        var results=await Task.WhenAll(repo.SaveWarehouseAsync("foundation-test","foundation-account",snapshot,next,bank,nextBank,[]),repo.SaveWarehouseAsync("foundation-test","foundation-account",snapshot,next,bank,nextBank,[]));
        test("concurrent bank transfers have exactly one winner",results.Count(r=>r==InventorySaveResult.Saved)==1);
        snapshot=next;bank=nextBank;
        cmd.CommandText="ALTER TABLE USERDATA ADD CONSTRAINT fail_bank_character CHECK(strUserID<>'foundation-test' OR Gold<>880)";await cmd.ExecuteNonQueryAsync();
        var failed=false;try{await repo.SaveWarehouseAsync("foundation-test","foundation-account",snapshot,snapshot with {Gold=880},bank,bank with {Gold=1120},[]);}catch(MySqlException){failed=true;}
        cmd.CommandText="ALTER TABLE USERDATA DROP CONSTRAINT fail_bank_character";await cmd.ExecuteNonQueryAsync();
        test("character SQL failure rolls back already-written bank funds",failed&&(await repo.LoadWarehouseAsync("foundation-account")).Gold==1110);
        cmd.CommandText="SELECT Gold,Hp,Mp,Level,Exp,strItem,strSerial FROM USERDATA WHERE strUserID='foundation-test'";
        await using(var r=await cmd.ExecuteReaderAsync()){await r.ReadAsync();test("bank transfer leaves unrelated profile and opaque item tails unchanged",Convert.ToInt32(r[0])==890&&Convert.ToInt32(r[1])==77&&Convert.ToInt32(r[2])==33&&Convert.ToInt32(r[3])==80&&Convert.ToInt32(r[4])==123&&((byte[])r[5]).SequenceEqual(blobs.Items)&&((byte[])r[6]).SequenceEqual(blobs.Serials));}
        test("bank ownership mismatch refused",await repo.SaveWarehouseAsync("foundation-test","preview-account",snapshot,snapshot,bank,bank,[])!=InventorySaveResult.Saved);
        cmd.CommandText="INSERT INTO SEALED_ITEMS(nItemID,nItemSerial,strUserID) VALUES(389014000,12345,'foundation-test')";await cmd.ExecuteNonQueryAsync();
        test("protected warehouse source refused transactionally",await repo.SaveWarehouseAsync("foundation-test","foundation-account",snapshot,snapshot,bank,bank,[(389014000,12345)])!=InventorySaveResult.Saved);
        cmd.CommandText="DELETE FROM SEALED_ITEMS WHERE strUserID='foundation-test'; ALTER TABLE WAREHOUSE ADD WarehouseDataTime blob NULL; ALTER TABLE USERDATA ADD strItemTime blob NULL";await cmd.ExecuteNonQueryAsync();
        var timed=new byte[75*4];System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(timed.AsSpan(14*4),123);
        cmd.CommandText="UPDATE USERDATA SET strItemTime=@time WHERE strUserID='foundation-test'";cmd.Parameters.AddWithValue("@time",timed);await cmd.ExecuteNonQueryAsync();cmd.Parameters.Clear();
        bank=await repo.LoadWarehouseAsync("foundation-account");var moved=inv.ToArray();moved[14]=ItemSlotData.Empty;var movedBlobs=ItemBlobs.Serialize(moved);
        test("optional inventory expiration cannot be silently stripped by banking",await repo.SaveWarehouseAsync("foundation-test","foundation-account",snapshot,new(movedBlobs.Items,movedBlobs.Serials,snapshot.Gold),bank,bank,[])!=InventorySaveResult.Saved);
        cmd.CommandText="UPDATE USERDATA SET strItemTime=NULL WHERE strUserID='foundation-test'";await cmd.ExecuteNonQueryAsync();
        var item=new ShopItem(389014000,1,0,0,0,1,5,1);var transfer=WarehouseRules.Propose(new(2,10001,item.Id,7,0,23,3),item,inv,stored,snapshot.Gold,bank.Gold,50,1000)!;
        var tb=ItemBlobs.Serialize(transfer.Inventory);nextBank=MariaDbCharacterRepository.WarehouseProposal(bank,transfer.Warehouse,transfer.Bank);
        test("item deposit commits inventory and page-eight bank slot together",await repo.SaveWarehouseAsync("foundation-test","foundation-account",snapshot,new(tb.Items,tb.Serials,transfer.Gold),bank,nextBank,[(item.Id,12345)])==InventorySaveResult.Saved);
        var reloaded=await repo.LoadWarehouseAsync("foundation-account");var loaded=ItemBlobs.Parse(reloaded.Items,reloaded.Serials,reloaded.Times,192);
        test("bank item count/durability/serial survive reconnect",loaded[191]==new ItemSlotData(item.Id,1,3,12345,0)&&reloaded.HasTimes);
        cmd.CommandText="INSERT INTO WAREHOUSE SELECT * FROM WAREHOUSE WHERE strAccountID='foundation-account'";await cmd.ExecuteNonQueryAsync();
        var duplicate=false;try{await repo.LoadWarehouseAsync("foundation-account");}catch(InvalidDataException){duplicate=true;}
        test("duplicate warehouse identity refused without choosing arbitrary row",duplicate);
        test("duplicate warehouse also refused by transfer transaction",await repo.SaveWarehouseAsync("foundation-test","foundation-account",new(tb.Items,tb.Serials,transfer.Gold),new(tb.Items,tb.Serials,transfer.Gold),reloaded,reloaded,[])!=InventorySaveResult.Saved);
    }
}
