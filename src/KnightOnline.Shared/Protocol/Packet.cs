using System.Binary;
using System.Text;

namespace KnightOnline.Shared.Protocol;

/// <summary>
/// Port of shared/ByteBuffer.h + shared/Packet.h.
///
/// One buffer with a write position (implicit, the end of the data) and a read
/// position, little-endian fields, and KO's two string modes:
/// <see cref="DByte"/> (uint16 length prefix, the default) and
/// <see cref="SByte"/> (uint8 length prefix).
///
/// Behaviour ported deliberately from the C++:
///  - Reads past the end return 0 / zero-filled data instead of throwing.
///    Many original handlers rely on this soft-failure behaviour.
///  - Strings are single-byte encoded. <see cref="Encoding.Latin1"/> is used
///    because it maps bytes 0-255 1:1, so any byte sequence the client sends
///    round-trips losslessly (the C++ code passed raw char* through).
///    NOTE: re-evaluate (CP949) if a Korean client is ever targeted.
/// </summary>
public sealed class Packet
{
    private byte[] _buffer;
    private int _length;    // payload bytes written (== C++ ByteBuffer::size())
    private int _readPos;   // == C++ _rpos
    private bool _doubleByteStrings = true;

    public byte Opcode { get; set; }

    /// <summary>Payload length, excluding the opcode byte (C++ Packet::size()).</summary>
    public int Length => _length;

    public int ReadPosition
    {
        get => _readPos;
        set => _readPos = value;
    }

    public int RemainingToRead => Math.Max(0, _length - _readPos);

    public ReadOnlySpan<byte> Payload => _buffer.AsSpan(0, _length);

    public Packet(byte opcode, int capacity = 128)
    {
        Opcode = opcode;
        _buffer = new byte[Math.Max(32, capacity)];
    }

    /// <summary>Port of Packet(opcode, subOpcode): first payload byte is the sub-opcode.</summary>
    public Packet(byte opcode, byte subOpcode) : this(opcode)
    {
        WriteByte(subOpcode);
    }

    private Packet(byte opcode, byte[] ownedBuffer, int length)
    {
        Opcode = opcode;
        _buffer = ownedBuffer;
        _length = length;
    }

    /// <summary>Copies received payload bytes into a packet positioned for reading.</summary>
    public static Packet FromReceived(byte opcode, ReadOnlySpan<byte> payload)
    {
        var copy = new byte[Math.Max(32, payload.Length)];
        payload.CopyTo(copy);
        return new Packet(opcode, copy, payload.Length);
    }

    /// <summary>Port of Packet::Initialize().</summary>
    public void Initialize(byte opcode)
    {
        Opcode = opcode;
        _length = 0;
        _readPos = 0;
    }

    public void Clear()
    {
        _length = 0;
        _readPos = 0;
    }

    // ---- KO string mode flags (ByteBuffer::SByte / DByte) ----

    public void SByte() => _doubleByteStrings = false;
    public void DByte() => _doubleByteStrings = true;

    // ---- Writing ----

    private Span<byte> Reserve(int count)
    {
        if (_length + count > _buffer.Length)
        {
            var newSize = Math.Max(_buffer.Length * 2, _length + count);
            Array.Resize(ref _buffer, newSize);
        }
        var span = _buffer.AsSpan(_length, count);
        _length += count;
        return span;
    }

    public Packet WriteByte(byte value)
    {
        Reserve(1)[0] = value;
        return this;
    }

    public Packet WriteSByte(sbyte value) => WriteByte(unchecked((byte)value));

    public Packet WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

    public Packet WriteUInt16(ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(Reserve(2), value);
        return this;
    }

    public Packet WriteInt16(short value) => WriteUInt16(unchecked((ushort)value));

    public Packet WriteUInt32(uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(Reserve(4), value);
        return this;
    }

    public Packet WriteInt32(int value) => WriteUInt32(unchecked((uint)value));

    public Packet WriteUInt64(ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(Reserve(8), value);
        return this;
    }

    public Packet WriteInt64(long value) => WriteUInt64(unchecked((ulong)value));

    public Packet WriteSingle(float value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(Reserve(4), value);
        return this;
    }

    public Packet WriteBytes(ReadOnlySpan<byte> data)
    {
        data.CopyTo(Reserve(data.Length));
        return this;
    }

    /// <summary>Appends another packet's payload (C++: append(contents, size)).</summary>
    public Packet WritePayloadOf(Packet other) => WriteBytes(other.Payload);

    public Packet WriteString(string value)
    {
        var bytes = Encoding.Latin1.GetBytes(value);
        if (_doubleByteStrings)
            WriteUInt16((ushort)bytes.Length);
        else
            WriteByte((byte)bytes.Length);
        return WriteBytes(bytes);
    }

    /// <summary>Port of ByteBuffer::put() - overwrite bytes already written.</summary>
    public void Put(int position, ReadOnlySpan<byte> data)
    {
        if (position < 0 || position + data.Length > _length)
            return;
        data.CopyTo(_buffer.AsSpan(position, data.Length));
    }

    public void PutUInt16(int position, ushort value)
    {
        Span<byte> tmp = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(tmp, value);
        Put(position, tmp);
    }

    // ---- Reading (soft-fail like the C++: out-of-range reads produce zeroes) ----

    private ReadOnlySpan<byte> ReadSpan(int count)
    {
        if (count <= 0)
            return default;

        var available = Math.Min(count, RemainingToRead);
        var span = _buffer.AsSpan(_readPos, available);
        _readPos += count; // C++ advances rpos by the full amount even on overrun
        _readPos = Math.Min(_readPos, _length);
        return span;
    }

    public byte ReadByte()
    {
        var span = ReadSpan(1);
        return span.Length == 1 ? span[0] : (byte)0;
    }

    public sbyte ReadSByte() => unchecked((sbyte)ReadByte());

    public bool ReadBool() => ReadByte() > 0;

    public ushort ReadUInt16()
    {
        var span = ReadSpan(2);
        return span.Length == 2 ? BinaryPrimitives.ReadUInt16LittleEndian(span) : (ushort)0;
    }

    public short ReadInt16() => unchecked((short)ReadUInt16());

    public uint ReadUInt32()
    {
        var span = ReadSpan(4);
        return span.Length == 4 ? BinaryPrimitives.ReadUInt32LittleEndian(span) : 0u;
    }

    public int ReadInt32() => unchecked((int)ReadUInt32());

    public ulong ReadUInt64()
    {
        var span = ReadSpan(8);
        return span.Length == 8 ? BinaryPrimitives.ReadUInt64LittleEndian(span) : 0ul;
    }

    public long ReadInt64() => unchecked((long)ReadUInt64());

    public float ReadSingle()
    {
        var span = ReadSpan(4);
        return span.Length == 4 ? BinaryPrimitives.ReadSingleLittleEndian(span) : 0f;
    }

    /// <summary>Copies up to <paramref name="count"/> bytes; zero-fills the rest (C++ read()).</summary>
    public byte[] ReadBytes(int count)
    {
        var result = new byte[count];
        ReadSpan(count).CopyTo(result);
        return result;
    }

    public string ReadString()
    {
        int len = _doubleByteStrings ? ReadUInt16() : ReadByte();
        if (len <= 0)
            return string.Empty;

        // C++ only copies the string if it fully fits in the remaining buffer.
        if (len > RemainingToRead)
        {
            // still consumes nothing beyond the length prefix, like the original
            return string.Empty;
        }

        return Encoding.Latin1.GetString(ReadSpan(len));
    }

    public byte[] ToArray() => Payload.ToArray();
}
