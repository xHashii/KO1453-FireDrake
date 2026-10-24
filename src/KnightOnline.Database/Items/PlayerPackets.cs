using KnightOnline.Shared.Protocol;
namespace KnightOnline.Database.Items;
public sealed record PlayerAppearance(string Name,byte Nation,byte Level,byte Race,ushort Class,
    ushort X,ushort Z,short Y,byte Face,byte Hair,byte Pose,byte Authority,short Direction,byte Rank,byte Zone,IReadOnlyList<ItemSlotData> Items);
public static class PlayerPackets
{
    /// <summary>CUser::GetUserInfo's clanless 1453 layout. This is NOT the
    /// character-select or MYINFO layout; all three have different item orders.</summary>
    public static byte[] Info(PlayerAppearance u)
    {
        var p=new Packet(WizOpcode.UserInout);p.SByte();
        p.WriteString(u.Name).WriteByte(u.Nation).WriteInt16(0).WriteByte(0)
            .WriteUInt32(0).WriteUInt16(0).WriteByte(0).WriteUInt16(ushort.MaxValue)
            .WriteByte(u.Level).WriteByte(u.Race).WriteUInt16(u.Class)
            .WriteUInt16(u.X).WriteUInt16(u.Z).WriteInt16(u.Y)
            .WriteByte(u.Face).WriteByte(u.Hair).WriteByte(u.Pose)
            .WriteUInt32(1).WriteByte(1).WriteByte(u.Authority).WriteByte(0).WriteByte(0).WriteByte(0)
            .WriteInt16(u.Direction).WriteByte(0).WriteByte(u.Rank).WriteSByte(-1).WriteSByte(-1);
        foreach(var index in new[]{4,10,1,12,13,5,6,8,46,43})
        { var s=index<u.Items.Count?u.Items[index]:ItemSlotData.Empty;p.WriteUInt32(s.ItemId).WriteInt16(s.Durability).WriteByte(0); }
        return p.WriteByte(u.Zone).Payload.ToArray();
    }
}
