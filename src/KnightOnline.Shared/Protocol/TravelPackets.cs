namespace KnightOnline.Shared.Protocol;

/// <summary>1453 travel replies from CharacterMovementHandler.cpp. Stored positions are world * 100.</summary>
public static class TravelPackets
{
    public static Packet Warp(int px, int pz) => new Packet(WizOpcode.Warp)
        .WriteUInt16(WirePosition(px)).WriteUInt16(WirePosition(pz));

    public static Packet ChangeZone(ushort zone, int px, int pz, int py) =>
        new Packet(WizOpcode.ZoneChange, (byte)3).WriteUInt16(zone)
            .WriteUInt16(WirePosition(px)).WriteUInt16(WirePosition(pz))
            .WriteUInt16(unchecked((ushort)(py / 10))).WriteByte(0);

    private static ushort WirePosition(int stored)
    {
        if (stored < 0 || stored / 10 > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(stored));
        return (ushort)(stored / 10);
    }
}
