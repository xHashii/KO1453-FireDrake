using KnightOnline.Shared.Protocol;
namespace KnightOnline.Database.Items;
public sealed record PlayerAppearance(string Name,byte Nation,byte Level,byte Race,ushort Class,
    ushort X,ushort Z,short Y,byte Face,byte Hair,byte Pose,byte Authority,short Direction,byte Rank,byte Zone,IReadOnlyList<ItemSlotData> Items);
public static class PlayerPackets
{
    // Some 1453 clients suppress authority-0 remote actors for ordinary viewers.
    // Public appearance is not authorization: MYINFO and server-side permissions
    // retain the real authority. The presence hub enforces hiding; opt-in visible GMs need a renderable model.
    /// <summary>Native 006AB660: subcode 0 followed by uint32 current XP.</summary>
    public static Packet Experience(uint experience) => new Packet(WizOpcode.ExpChange).WriteByte(0).WriteUInt32(experience);
    public static byte PublicAuthority(byte authority) => authority is 0 or 250 ? (byte)1 : authority;
    /// <summary>Native 1453 clanless remote-player layout. This is NOT the
    /// character-select or MYINFO layout; all three have different item orders.</summary>
    public static byte[] Info(PlayerAppearance u,Func<uint,bool>? knownItem = null)
    {
        var p=new Packet(WizOpcode.UserInout);p.SByte();
        p.WriteString(u.Name).WriteByte(u.Nation).WriteInt16(0).WriteByte(0)
            .WriteUInt16(0) // alliance
            .WriteByte(0).WriteByte(0).WriteByte(0) // clan name length, grade, ranking
            .WriteUInt16(0).WriteUInt16(ushort.MaxValue) // mark, cape
            .WriteByte(u.Level).WriteByte(u.Race).WriteUInt16(u.Class)
            .WriteUInt16(u.X).WriteUInt16(u.Z).WriteInt16(u.Y)
            .WriteByte(u.Face).WriteByte(u.Hair).WriteByte(u.Pose)
            .WriteUInt32(1).WriteByte(1).WriteByte(PublicAuthority(u.Authority)).WriteByte(0).WriteByte(0).WriteByte(0)
            .WriteInt16(u.Direction).WriteByte(0).WriteByte(u.Rank).WriteSByte(-1).WriteSByte(-1);
        foreach(var index in new[]{4,10,1,12,13,5,6,8,46,43})
        { var s=index<u.Items.Count?u.Items[index]:ItemSlotData.Empty; if(s.IsEmpty || (knownItem is not null && !knownItem(s.ItemId))) s=ItemSlotData.Empty; p.WriteUInt32(s.ItemId).WriteInt16(s.Durability).WriteByte(0); }
        return p.WriteByte(u.Zone).Payload.ToArray();
    }
}
