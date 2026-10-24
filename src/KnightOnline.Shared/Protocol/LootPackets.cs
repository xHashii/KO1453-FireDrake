namespace KnightOnline.Shared.Protocol;

public static class LootPackets
{
    public static Packet Spawn(ushort npc, uint bundle) => new Packet(WizOpcode.ItemDrop).WriteUInt16(npc).WriteUInt32(bundle);
    public static Packet Open(IReadOnlyList<(uint Item, ushort Count)> entries)
    {
        var reply = new Packet(WizOpcode.BundleOpenReq);
        for (var i=0;i<6;i++) reply.WriteUInt32(i < entries.Count ? entries[i].Item : 0).WriteUInt16(i < entries.Count ? entries[i].Count : (ushort)0);
        return reply;
    }
    public static Packet Taken(uint item, ushort count, int gold, byte position=255) => new Packet(WizOpcode.ItemGet)
        .WriteByte(1).WriteByte(position).WriteUInt32(item).WriteUInt16(count).WriteUInt32((uint)gold);
}
