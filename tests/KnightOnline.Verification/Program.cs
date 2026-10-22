using System.Text;
using KnightOnline.Shared.Compression;
using KnightOnline.Shared.Configuration;
using KnightOnline.Shared.Crypto;
using KnightOnline.Shared.Protocol;
using KnightOnline.Verification;

// Verifies the C# wire-protocol ports byte-for-byte against the original C++
// (see GoldenVectors.cs). Exit code 0 = everything matches.

var passed = 0;
var failed = 0;

void Check(string name, bool condition, string? detail = null)
{
    if (condition)
    {
        passed++;
        Console.WriteLine($"PASS  {name}");
    }
    else
    {
        failed++;
        Console.WriteLine($"FAIL  {name}" + (detail is null ? "" : $"\n      {detail}"));
    }
}

void CheckHex(string name, ReadOnlySpan<byte> actual, string expectedHex)
{
    var expected = Convert.FromHexString(expectedHex);
    Check(name, actual.SequenceEqual(expected),
        $"got  {Convert.ToHexString(actual)}\n      want {expectedHex}");
}

void CheckUIntHex(string name, uint actual, string expectedHex)
{
    var expected = Convert.ToUInt32(expectedHex, 16);
    Check(name, actual == expected, $"got {actual:X8}, want {expectedHex}");
}

// Same deterministic PRNG the C golden harness used (LCG fill()).
static byte[] Fill(int count, uint seed)
{
    var data = new byte[count];
    var s = seed;
    for (var i = 0; i < count; i++)
    {
        s = s * 1103515245u + 12345u;
        data[i] = (byte)((s >> 16) & 0xFF);
    }
    return data;
}

const ulong GoldenPublicKey = 0x1122334455667788UL;

JvCryption MakeCrypto()
{
    var crypto = new JvCryption();
    crypto.SetPublicKey(GoldenPublicKey);
    crypto.Init();
    return crypto;
}

// ---------- KoCrc32 (shared/crc32.c) ----------
var digits = Encoding.ASCII.GetBytes("123456789");
CheckUIntHex("crc32 digits start=0", KoCrc32.Compute(digits, 0), GoldenVectors.CrcDigitsStart0);
CheckUIntHex("crc32 digits start=-1", KoCrc32.Compute(digits, 0xFFFFFFFFu), GoldenVectors.CrcDigitsStartFF);
CheckUIntHex("crc32 rand256", KoCrc32.Compute(Fill(256, 42), 0), GoldenVectors.CrcRand256Seed42);

// ---------- JvCryption (shared/JvCryption.cpp) ----------
{
    var crypto = MakeCrypto();

    var hello = Encoding.ASCII.GetBytes("Hello, KO!");
    crypto.EncryptFast(hello);
    CheckHex("jv encrypt 'Hello, KO!'", hello, GoldenVectors.JvHello);

    var rand100 = Fill(100, 7);
    CheckHex("golden rand100 input reproduced", rand100, GoldenVectors.JvRand100In);
    crypto.EncryptFast(rand100);
    CheckHex("jv encrypt rand100", rand100, GoldenVectors.JvRand100Out);
    crypto.DecryptFast(rand100); // symmetric
    CheckHex("jv decrypt == encrypt (roundtrip)", rand100, GoldenVectors.JvRand100In);

    var one = new byte[] { 0xAB };
    crypto.EncryptFast(one);
    CheckHex("jv encrypt single byte", one, GoldenVectors.JvOneAb);

    // GenerateKey must never return 0
    var keyGen = new JvCryption();
    var allNonZero = true;
    for (var i = 0; i < 100; i++)
        allNonZero &= keyGen.GenerateKey() != 0 && keyGen.PublicKey != 0;
    Check("jv GenerateKey never 0", allNonZero);
}

// ---------- KoPacketCodec: server -> client encrypted block ----------
{
    var crypto = MakeCrypto();
    var packet = new Packet(0x10); // WIZ_CHAT with payload "hello"
    packet.WriteBytes(Encoding.ASCII.GetBytes("hello"));

    var frame = KoPacketCodec.Encode(packet, crypto, useCrypto: true, sequence: 7);

    // Frame: AA 55 | len | payload(11) | 55 AA ; payload must be the golden block
    Check("encode frame header/footer",
        frame[0] == 0xAA && frame[1] == 0x55 && frame[^2] == 0x55 && frame[^1] == 0xAA);
    Check("encode frame length field", (frame[2] | (frame[3] << 8)) == 11);
    CheckHex("encode encrypted payload == C++ Send()", frame.AsSpan(4, 11), GoldenVectors.JvSendBlock);
}

// ---------- KoPacketCodec: client -> server encrypted packet ----------
{
    var crypto = MakeCrypto();
    var encrypted = Convert.FromHexString(GoldenVectors.ClientPacketEnc);

    uint sequence = 0;
    var packet = KoPacketCodec.DecodePayload(encrypted, crypto, useCrypto: true, ref sequence);

    Check("decode client packet accepted", packet is not null);
    Check("decode client packet opcode", packet?.Opcode == 0xF3); // LS_LOGIN_REQ
    Check("decode client packet payload", packet is not null &&
        Encoding.ASCII.GetString(packet.Payload) == "abcde");
    Check("decode client packet sequence advanced", sequence == 1);

    // Same packet again must be rejected (sequence replay), like the original.
    var replay = KoPacketCodec.DecodePayload(encrypted, crypto, useCrypto: true, ref sequence);
    Check("decode replayed sequence rejected", replay is null);

    // Flipping any byte must break the CRC (random byte, keep header bytes out).
    var corrupt = encrypted.ToArray();
    corrupt[6] ^= 0x40;
    uint seq2 = 0;
    var rejected = KoPacketCodec.DecodePayload(corrupt, crypto, useCrypto: true, ref seq2);
    Check("decode corrupted packet rejected (CRC)", rejected is null);
}

// ---------- KoPacketCodec: plain roundtrip ----------
{
    var packet = new Packet(WizOpcode.Chat, (byte)1);
    packet.WriteString("FireDrake");
    packet.WriteUInt32(0xDEADBEEF);

    var frame = KoPacketCodec.Encode(packet, null, useCrypto: false, sequence: 0);
    var payload = frame.AsSpan(4, frame.Length - KoPacketCodec.FrameOverhead);

    uint sequence = 0;
    var decoded = KoPacketCodec.DecodePayload(payload, null, useCrypto: false, ref sequence)!;
    Check("plain roundtrip opcode", decoded.Opcode == WizOpcode.Chat);
    Check("plain roundtrip sub-opcode", decoded.ReadByte() == 1);
    Check("plain roundtrip string", decoded.ReadString() == "FireDrake");
    Check("plain roundtrip uint32", decoded.ReadUInt32() == 0xDEADBEEF);
}

// ---------- Packet string modes ----------
{
    var p = new Packet(1);
    p.WriteString("AB");          // DByte: uint16 length
    p.SByte();
    p.WriteString("CDE");         // SByte: uint8 length

    var bytes = p.ToArray();
    CheckHex("packet DByte/SByte framing", bytes, "0200414203434445");

    var q = Packet.FromReceived(1, bytes);
    var first = q.ReadString();
    q.SByte();
    var second = q.ReadString();
    Check("packet DByte/SByte readback", first == "AB" && second == "CDE");

    // Reading past the end returns zeroes instead of throwing (C++ behaviour)
    var empty = Packet.FromReceived(1, ReadOnlySpan<byte>.Empty);
    Check("packet overrun reads are soft", empty.ReadUInt32() == 0 && empty.ReadString() == "");
}

// ---------- LZF (shared/lzf.c) ----------
{
    CheckHex("lzf compress zeros1000", Lzf.CompressToArray(new byte[1000]), GoldenVectors.LzfZeros1000);

    var text = Encoding.ASCII.GetBytes(
        "KnightOnline KnightOnline KnightOnline Reign of the Fire Drake 1453 1453 1453!");
    CheckHex("lzf compress text", Lzf.CompressToArray(text), GoldenVectors.LzfText);

    var rand2000 = Fill(2000, 99);
    CheckHex("lzf compress rand2000", Lzf.CompressToArray(rand2000), GoldenVectors.LzfRand2000);

    var pattern = new byte[600];
    for (var i = 0; i < pattern.Length; i++) pattern[i] = (byte)(i % 251);
    CheckHex("lzf compress pattern600", Lzf.CompressToArray(pattern), GoldenVectors.LzfPattern600);

    // Decompress the ORIGINAL's output and compare with the known inputs
    Check("lzf decompress zeros1000",
        Lzf.DecompressToArray(Convert.FromHexString(GoldenVectors.LzfZeros1000), 1000)
            .SequenceEqual(new byte[1000]));
    Check("lzf decompress text",
        Lzf.DecompressToArray(Convert.FromHexString(GoldenVectors.LzfText), text.Length)
            .SequenceEqual(text));
    Check("lzf decompress rand2000",
        Lzf.DecompressToArray(Convert.FromHexString(GoldenVectors.LzfRand2000), 2000)
            .SequenceEqual(rand2000));

    // Roundtrip fuzz: deterministic inputs of varying shapes
    var shapes = new List<byte[]>
    {
        new byte[] { 1 },
        new byte[] { 1, 2, 3 },
        Fill(17, 1), Fill(300, 5), Fill(5000, 123),
        Encoding.ASCII.GetBytes(new string('A', 1000)),
        Enumerable.Range(0, 2000).Select(i => (byte)(i % 7)).ToArray(),
    };
    var fuzzOk = shapes.All(data =>
    {
        var compressed = Lzf.CompressToArray(data);
        return compressed.Length > 0 &&
               Lzf.DecompressToArray(compressed, data.Length).SequenceEqual(data);
    });
    Check("lzf roundtrip fuzz", fuzzOk);
}

// ---------- SendCompressed wrapper (KOSocket::SendCompressed) ----------
{
    var small = new Packet(WizOpcode.Myinfo);
    small.WriteBytes(new byte[100]);
    Check("compress skips < 500 bytes", KoPacketCodec.CompressIfNeeded(small) is null);

    var big = new Packet(WizOpcode.Myinfo);
    big.WriteBytes(new byte[600]);
    var wrapped = KoPacketCodec.CompressIfNeeded(big);
    Check("compress wraps >= 500 bytes", wrapped is not null && wrapped.Opcode == WizOpcode.CompressPacket);
    if (wrapped is not null)
    {
        var compLen = wrapped.ReadUInt32();
        var origLen = wrapped.ReadUInt32();
        var crc = wrapped.ReadUInt32();
        var comp = wrapped.ReadBytes((int)compLen);
        var restored = Lzf.DecompressToArray(comp, (int)origLen);
        Check("compress wrapper roundtrip",
            origLen == 601 && KoCrc32.Compute(restored, 0) == crc &&
            restored[0] == WizOpcode.Myinfo && restored.Skip(1).All(b => b == 0));
    }
}

// ---------- IniFile ----------
{
    var tempIni = Path.Combine(Path.GetTempPath(), $"ko-verify-{Guid.NewGuid():N}.ini");
    File.WriteAllText(tempIni, """
        [SETTINGS]
        PORT=15100
        ; comment
        [SERVER_LIST]
        NAME_00=Test|Server 1
        """);
    var ini = new IniFile(tempIni);
    Check("ini reads int", ini.GetInt("SETTINGS", "PORT", 0) == 15100);
    Check("ini is case-insensitive", ini.GetString("server_list", "name_00", "") == "Test|Server 1");
    Check("ini falls back to default", ini.GetInt("NOPE", "NOPE", 42) == 42);
    File.Delete(tempIni);

    var missing = new IniFile(Path.Combine(Path.GetTempPath(), "ko-does-not-exist.ini"));
    Check("ini missing file uses defaults", !missing.FileExists && missing.GetString("A", "B", "x") == "x");
}

Console.WriteLine($"\n{passed} passed, {failed} failed.");
return failed == 0 ? 0 : 1;
