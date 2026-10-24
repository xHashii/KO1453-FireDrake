using KnightOnline.Shared.Protocol;

namespace KnightOnline.Shared.Diagnostics;

public static class PacketTrace
{
    /// <summary>Passwords can occur in login and character-deletion packets.
    /// Never log their raw payload, including truncated or malformed packets.</summary>
    public static string GamePayload(Packet packet) => packet.Opcode is WizOpcode.Login or WizOpcode.DelChar
        ? "[redacted: credential-bearing packet]"
        : Convert.ToHexString(packet.Payload);
}
