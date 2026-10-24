using KnightOnline.Shared.Protocol;
namespace KnightOnline.Database.Items;
public sealed record WarehouseRequest(byte Action, ushort Npc, uint Item, byte Page, byte Source, byte Destination, uint Count, bool HasNpc = true);
public sealed record WarehouseChange(ItemSlotData[] Inventory,ItemSlotData[] Warehouse,int Gold,int Bank);
public static class WarehouseRules
{
    /// <summary>NPC_WAREHOUSE sends WAREHOUSE_REQ, not WAREHOUSE_OPEN.
    /// The installed client supplies the InnHostess services menu text/options.</summary>
    public static Packet Menu() => new Packet(WizOpcode.Warehouse).WriteByte(0x10);
    // An omitted NPC is never a remote-banking authorization: the handler must
    // also revalidate the existing selected NPC, open snapshot, zone and range.
    public static bool MatchesSelectedNpc(WarehouseRequest request,ushort selected) =>
        selected!=0 && (!request.HasNpc || request.Npc==selected);
    public static int OpenFee(ushort zone) => zone is 71 or 72 or 73 ? 10000 : 0;
    public static bool TryRead(Packet p,out WarehouseRequest r)
    {
        r=new(0,0,0,0,0,0,0); if(p.RemainingToRead<1) return false;
        var a=p.ReadByte(); if(a==1 && p.RemainingToRead==0) {r=r with {Action=1}; return true;}
        if(a is <2 or >5) return false;
        var explicitLength=a is 2 or 3 ? 13 : 9;
        var hasNpc=p.RemainingToRead==explicitLength;
        if(!hasNpc && p.RemainingToRead!=explicitLength-2) return false;
        var npc=hasNpc?p.ReadUInt16():(ushort)0;
        r=new(a,npc,p.ReadUInt32(),p.ReadByte(),p.ReadByte(),p.ReadByte(),a is 2 or 3?p.ReadUInt32():0,hasNpc); return true;
    }
    public static WarehouseChange? Propose(WarehouseRequest r,ShopItem? item,IReadOnlyList<ItemSlotData> inventory,IReadOnlyList<ItemSlotData> warehouse,int gold,int bank,long weight,int maxWeight)
    {
        if(inventory.Count<42 || warehouse.Count!=192 || gold is <0 or >ShopRules.CoinMax || bank is <0 or >ShopRules.CoinMax || r.Action is <2 or >5 || r.Page>=8) return null;
        var inv=inventory.ToArray(); var wh=warehouse.ToArray();
        if(r.Item==900000000 && r.Action is 2 or 3)
        {
            if(r.Count==0 || r.Count>ShopRules.CoinMax) return null;
            var delta=(long)r.Count*(r.Action==2?1:-1);
            if(gold-delta is <0 or >ShopRules.CoinMax || bank+delta is <0 or >ShopRules.CoinMax) return null;
            return new(inv,wh,(int)(gold-delta),(int)(bank+delta));
        }
        if(item is null || item.Id!=r.Item || r.Item==0 || r.Item>=900000001) return null;
        var srcArray=r.Action is 2 or 5?inv:wh; var dstArray=r.Action is 3 or 5?inv:wh;
        if(r.Source>=(r.Action is 2 or 5?28:24) || r.Destination>=(r.Action is 3 or 5?28:24)) return null;
        var src=r.Action is 2 or 5?14+r.Source:24*r.Page+r.Source;
        var dst=r.Action is 3 or 5?14+r.Destination:24*r.Page+r.Destination;
        if(ReferenceEquals(srcArray,dstArray) && src==dst) return null;
        var sourceItem=srcArray[src]; var to=dstArray[dst];
        var count=r.Action is 4 or 5?(uint)Math.Max(0,(int)sourceItem.Count):r.Count;
        if(sourceItem.ItemId!=r.Item || sourceItem.Count<=0 || sourceItem.Count>9999 || count==0 || count>(uint)sourceItem.Count || sourceItem.ExpirationUnixTime!=0 || to.ExpirationUnixTime!=0 ||
            (item.Countable==0 && (sourceItem.Count!=1 || count!=1)) || (to.ItemId!=0 && (r.Action is 4 or 5 || item.Countable==0 || to.ItemId!=sourceItem.ItemId || to.Count<=0 || to.Durability!=sourceItem.Durability || to.Count+count>9999))) return null;
        if(r.Action==3 && (item.Weight<0 || weight<0 || weight+(long)item.Weight*count>maxWeight)) return null;
        dstArray[dst]=to.ItemId==0?(sourceItem with {Count=(short)count}):(to with {Count=(short)(to.Count+count)});
        srcArray[src]=count==sourceItem.Count?ItemSlotData.Empty:(sourceItem with {Count=(short)(sourceItem.Count-count)});
        return new(inv,wh,gold,bank);
    }
    public static Packet Result(byte action,bool success)=>new Packet(WizOpcode.Warehouse).WriteByte(action).WriteByte(success?(byte)1:(byte)0);
    public static Packet Open(int bank,IEnumerable<ItemSlotData> slots)
    {
        var all=slots.ToArray(); if(all.Length!=192) throw new ArgumentException("Warehouse requires 192 slots.");
        var p=Result(1,true).WriteUInt32((uint)bank);
        foreach(var s in all) p.WriteUInt32(s.ItemId).WriteInt16(s.Durability).WriteInt16(s.Count).WriteByte(0).WriteUInt32(0).WriteUInt32(s.ExpirationUnixTime);
        return p;
    }
}
