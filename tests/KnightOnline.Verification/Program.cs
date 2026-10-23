using System.Text;
using KnightOnline.Database.Admin;
using KnightOnline.Database.Items;
using KnightOnline.Database.Migrations;
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
        // Size for the worst case here: CompressToArray mirrors
        // KOSocket::SendCompressed (input + LZF_MARGIN), which by design
        // cannot hold incompressible data beyond ~4 KB - literal overhead
        // is 1 byte per 32, so 5000 random bytes need 5157. The original
        // lzf_compress returns 0 for that (verified against unmodified
        // shared/lzf.c) and the packet falls back to uncompressed; that
        // behaviour is asserted separately below.
        var scratch = new byte[data.Length + data.Length / 16 + Lzf.Margin];
        var written = Lzf.Compress(data, scratch);
        return written > 0 &&
               Lzf.DecompressToArray(scratch[..written], data.Length).SequenceEqual(data);
    });
    Check("lzf roundtrip fuzz", fuzzOk);

    // The KOSocket-sized buffer (input + LZF_MARGIN) cannot hold 5 KB of
    // incompressible data; like the original, compression reports failure.
    Check("lzf incompressible 5k exceeds KOSocket margin",
        Lzf.CompressToArray(Fill(5000, 123)).Length == 0);
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

// ---------- Item blobs (USERDATA.strItem etc., DBAgent.cpp layout) ----------
{
    // Hand-built slot: item 310110000 (0x127B03F0), durability 12345, count 7,
    // serial 0x1122334455667788, expiration 1700000000 (0x6553F100).
    var itemBlob = new byte[InventorySlots.Total * 8];
    var serialBlob = new byte[InventorySlots.Total * 8];
    var expirations = new byte[InventorySlots.Total * 4];
    // slot 6 = Right Hand
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(itemBlob.AsSpan(6 * 8), 310110000);
    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(itemBlob.AsSpan(6 * 8 + 4), 12345);
    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(itemBlob.AsSpan(6 * 8 + 6), 7);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(serialBlob.AsSpan(6 * 8), 0x1122334455667788UL);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(expirations.AsSpan(6 * 4), 1700000000u);

    var parsed = ItemBlobs.ParseExact(itemBlob, serialBlob, expirations);
    var slot6 = parsed.Slots[6];
    Check("item blob: slot fields", slot6.ItemId == 310110000 && slot6.Durability == 12345 &&
        slot6.Count == 7 && slot6.Serial == 0x1122334455667788UL && slot6.ExpirationUnixTime == 1700000000u);
    Check("item blob: empty slot", parsed.Slots[0].IsEmpty && parsed.SlotCount == 73);
    Check("item blob: slot naming", InventorySlots.Describe(6) == "Right Hand" &&
        InventorySlots.Describe(72) == "Magic Bag 2 slot 11");

    var (rtItems, rtSerials, rtExp) = ItemBlobs.Serialize(parsed.Slots);
    Check("item blob: roundtrip lossless",
        rtItems.SequenceEqual(itemBlob) && rtSerials.SequenceEqual(serialBlob) && rtExp.SequenceEqual(expirations));

    // Short/absent blobs read as empty trailing slots (C++ overrun semantics)
    var shortParse = ItemBlobs.Parse(itemBlob.AsSpan(0, 16), ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty, 73);
    Check("item blob: short blobs tolerated", shortParse[2].IsEmpty && shortParse[0] is not null);

    Check("item blob: load clamp", ItemBlobs.ClampForLoad(slot6, isCountable: false).Count == 1 &&
        ItemBlobs.ClampForLoad(slot6 with { Count = 30000 }, isCountable: true).Count == 9999);
}

// ---------- SqlScriptSplitter (migrations incl. procedures) ----------
{
    var script = """
        CREATE TABLE t (a VARCHAR(10) DEFAULT ';');
        -- a comment; with semicolon
        INSERT INTO t VALUES ('x;y'), ('it''s');
        DELIMITER //
        CREATE PROCEDURE p() BEGIN SET @x = 1; SET @y = 'a;b'; END //
        DELIMITER ;
        SELECT 1;
        """;
    var statements = SqlScriptSplitter.Split(script).ToList();
    Check("sql splitter: statement count", statements.Count == 4, string.Join(" | ", statements));
    Check("sql splitter: procedure kept whole",
        statements.Count == 4 && statements[2].Contains("SET @x = 1;") && statements[2].TrimEnd().EndsWith("END"));
    Check("sql splitter: strings respected",
        statements.Count == 4 && statements[1].Contains("'x;y'"));
}

// ---------- Migration script object scan (partial-apply repair) ----------
{
    // DatabaseMigrator uses this to clean up a script that died halfway: the
    // tables it creates are dropped when empty, the tables it fills are
    // truncated. Both lists come from this scan, so they must be exact.
    var schema = """
        SET FOREIGN_KEY_CHECKS=0;
        CREATE TABLE `_Aktivite`(
         `id` int AUTO_INCREMENT NOT NULL
        ) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

        CREATE TABLE IF NOT EXISTS `VERSION`(
         `sVersion` smallint NOT NULL
        ) ENGINE=InnoDB;
        CREATE TABLE bare_name (a INT);
        ALTER TABLE `VERSION` ALTER COLUMN `sVersion` SET DEFAULT 0;
        """;
    var created = MigrationScriptObjects.CreatedTables(schema);
    Check("migration scan: created tables",
        created.Count == 3 && created[0] == "_Aktivite" && created[1] == "VERSION" && created[2] == "bare_name",
        string.Join(" | ", created));

    var data = """
        INSERT `ITEM` (`ItemID`, `strName`) VALUES (1, 'Sword');
        INSERT INTO `ITEM` (`ItemID`) VALUES (2);
        INSERT `MAGIC` (`iNum`) VALUES (3);
        INSERT `ITEM` (`ItemID`) VALUES (4);
        """;
    var filled = MigrationScriptObjects.FilledTables(data);
    Check("migration scan: filled tables are distinct and ordered",
        filled.Count == 2 && filled[0] == "ITEM" && filled[1] == "MAGIC",
        string.Join(" | ", filled));
    Check("migration scan: a schema script fills nothing",
        MigrationScriptObjects.FilledTables(schema).Count == 0);
    Check("migration scan: identifiers are quoted for generated DDL",
        MigrationScriptObjects.Quote("a`b") == "`a``b`");
}

// ---------- Item serial generator (CGameServerDlg::GenerateItemSerial) ----------
{
    var gen = new ItemSerialGenerator(serverNo: 1);
    var a = gen.Next();
    var b = gen.Next();
    var now = DateTimeOffset.UtcNow;
    Check("serial: server number in top byte", (a >> 56) == 1);
    Check("serial: date fields packed (tm_mon 0-based, like the original)",
        ((a >> 48) & 0xFF) == (ulong)(now.Year % 100) &&
        ((a >> 40) & 0xFF) == (ulong)(now.Month - 1) &&
        ((a >> 32) & 0xFF) == (ulong)now.Day);
    Check("serial: counter increments by 1", (b & 0xFFFF) == ((a & 0xFFFF) + 1) % 0x10000);
}

Console.WriteLine($"\n{passed} passed, {failed} failed.");
return failed == 0 ? 0 : 1;
