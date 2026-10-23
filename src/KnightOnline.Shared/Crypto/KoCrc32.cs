namespace KnightOnline.Shared.Crypto;

/// <summary>
/// Port of shared/crc32.c (Gary S. Brown CRC-32, polynomial 0xEDB88320).
///
/// IMPORTANT: this is *not* the textbook "CRC-32/ISO-HDLC" wrapper - the
/// original takes a raw start value and applies NO final XOR. KO calls it
/// with start = 0 (compressed packets) and start = -1/0xFFFFFFFF (JvCryption
/// packet checksum). Keep those exact semantics.
/// </summary>
public static class KoCrc32
{
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    public static uint Compute(ReadOnlySpan<byte> data, uint startValue = 0)
    {
        var crc = startValue;
        foreach (var b in data)
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }
}
