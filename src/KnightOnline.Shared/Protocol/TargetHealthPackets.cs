namespace KnightOnline.Shared.Protocol;

public static class TargetHealthPackets
{
    // CUser::SendTargetHP: target ushort, echo byte, max/current int32, damage ushort.
    public static Packet Reply(ushort target, byte echo, int maxHp, int hp, ushort damage = 0) =>
        new Packet(WizOpcode.TargetHp).WriteUInt16(target).WriteByte(echo)
            .WriteInt32(Math.Max(0, maxHp)).WriteInt32(Math.Clamp(hp, 0, Math.Max(0, maxHp))).WriteUInt16(damage);
}
