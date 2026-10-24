namespace KnightOnline.Shared.Protocol;

public static class ChatPackets
{
    // ChatHandler.h ChatPacket::Construct: type, nation, int16 sender,
    // byte-length sender name, ushort-length message (legacy single-byte text).
    public static Packet Message(byte type, byte nation, ushort senderId, string name, string message)
    {
        var result = new Packet(WizOpcode.Chat).WriteByte(type).WriteByte(nation).WriteUInt16(senderId);
        result.SByte();
        result.WriteString(name);
        result.DByte();
        result.WriteString(message);
        return result;
    }

    public static bool TryRead(Packet packet, out byte type, out string message)
    {
        type = 0;
        message = "";
        if (packet.RemainingToRead < 3) return false;
        type = packet.ReadByte();
        var length = packet.ReadUInt16();
        if (length is 0 or > 128 || packet.RemainingToRead != length) return false;
        // Rewind the length so the normal single-byte string reader handles it.
        packet.ReadPosition -= 2;
        message = packet.ReadString();
        return !string.IsNullOrWhiteSpace(message) && !message.Any(char.IsControl);
    }
}
