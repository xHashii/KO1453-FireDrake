namespace KnightOnline.Shared.Protocol;

/// <summary>Explicit per-instance wire profile, not inferred from untrusted client packets.
/// 1534 is experimental: only source-verified handshake differences are implemented.</summary>
public static class ClientProtocol
{
    public static ushort Validate(int version) => version is 1453 or 1534
        ? (ushort)version : throw new ArgumentOutOfRangeException(nameof(version), "PROTOCOL_VERSION must be 1453 or 1534.");

    public static Packet VersionReply(byte opcode, ushort version, ulong? key = null)
    {
        var result = new Packet(opcode).WriteUInt16(version);
        if (key.HasValue) result.WriteUInt64(key.Value);
        return result;
    }

    // ForcePower/KODevelopers-1534 cd3c07d LoginSession::HandleServerlist:
    // >=1500 echoes a uint16 before the shared server list body.
    public static bool TryServerListPrefix(Packet request, ushort version, out Packet reply)
    {
        Validate(version);
        reply = new Packet(request.Opcode);
        if (version == 1534)
        {
            if (request.Length != 2) return false;
            reply.WriteUInt16(request.ReadUInt16());
        }
        // Legacy 1453 handler ignored request contents; preserve that behavior.
        return true;
    }
}
