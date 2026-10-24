using KnightOnline.Shared.Protocol;

namespace KnightOnline.Shared.Diagnostics;

public static class PacketTrace
{
    /// <summary>Passwords can occur in login, deletion and item/character seal packets.
    /// Never log their raw payload, including truncated or malformed packets.</summary>
    public static string GamePayload(Packet packet) => packet.Opcode is WizOpcode.Login or WizOpcode.DelChar
        || (packet.Opcode == WizOpcode.ItemUpgrade && (packet.Length == 0 || packet.Payload[0] is 8 or 9))
        ? "[redacted: credential-bearing packet]"
        : Convert.ToHexString(packet.Payload);
}
