using KnightOnline.Shared.Protocol;
namespace KnightOnline.Database.Items;

/// <summary>Source request layout. Parsing does not authorize a mutation; the
/// repository validates the authenticated owner, code, serial and snapshot.</summary>
public sealed class ItemSealRequest(byte action,uint item,byte position,string password)
{
    public byte Action { get; }=action;
    public uint Item { get; }=item;
    public byte Position { get; }=position;
    internal string Password { get; }=password;
    public override string ToString()=>$"ItemSeal(action={Action},item={Item},position={Position},password=redacted)";
}
public static class ItemSealPackets
{
    public static bool TryRead(Packet packet,out ItemSealRequest? request)
    {
        request=null;
        // Remaining payload starts after ITEM_UPGRADE subopcode 8.
        if(packet.RemainingToRead<10) return false;
        var action=packet.ReadByte();var marker=packet.ReadInt16();
        var item=packet.ReadUInt32();var position=packet.ReadByte();var length=packet.ReadUInt16();
        if(action is not (1 or 2) || marker!=-1 || item==0 || position>=28 || length is 0 or >8 || packet.RemainingToRead!=length) return false;
        packet.ReadPosition-=2;
        request=new(action,item,position,packet.ReadString()); return true;
    }
    // ReqSealItem includes item and position on database replies; success is 1.
    public static Packet Result(ItemSealRequest request, byte code) => new Packet(WizOpcode.ItemUpgrade)
        .WriteByte(8).WriteByte(request.Action).WriteByte(code).WriteUInt32(request.Item).WriteByte(request.Position);
    // Preflight refusal shape from SealHandler.cpp; no mutation authorized.
    public static Packet Refused(byte action)=>new Packet(WizOpcode.ItemUpgrade).WriteByte(8).WriteByte(action).WriteByte(2);
}
