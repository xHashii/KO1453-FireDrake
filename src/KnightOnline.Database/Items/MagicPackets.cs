using KnightOnline.Shared.Protocol;
namespace KnightOnline.Database.Items;
public sealed record MagicRequest(byte Stage, uint Skill, ushort Caster, ushort Target);
/// <summary>Common magic framing, not a potion classification. The definition
/// determines handler/cost/effect; self-targeting alone never selects a potion.</summary>
public static class MagicPackets
{
    public static bool TryRead(Packet packet, out MagicRequest request)
    {
        request = new(0, 0, 0, 0);
        // Observed 1453 client sends six data shorts; original C++ also accepts
        // a seventh via its soft over-read. Do not accept arbitrary truncation.
        if (packet.RemainingToRead is not (21 or 23)) return false;
        request = new(packet.ReadByte(), packet.ReadUInt32(), packet.ReadUInt16(), packet.ReadUInt16());
        return true;
    }

}
