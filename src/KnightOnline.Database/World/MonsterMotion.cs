using KnightOnline.Shared.Protocol;

namespace KnightOnline.Database.World;

/// <summary>Immutable, atomically published authoritative position.</summary>
public sealed record MonsterMotion(float X, float Y, float Z, float Speed, long Revision, string Life);

public static class MonsterMovementPackets
{
    // CNpc::MoveResult: id, X/Z/Y in decimetres, speed in decimetres/second.
    public static Packet Move(ushort id, MonsterMotion p) => new Packet(WizOpcode.NpcMove)
        .WriteUInt16(id).WriteUInt16((ushort)(p.X*10)).WriteUInt16((ushort)(p.Z*10))
        .WriteInt16((short)Math.Clamp((int)(p.Y*10),short.MinValue,short.MaxValue))
        .WriteUInt16((ushort)Math.Clamp((int)(p.Speed*10),0,ushort.MaxValue));
}
