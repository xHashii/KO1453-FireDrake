using System.Buffers.Binary;
using System.Security.Cryptography;

namespace KnightOnline.Shared.Crypto;

/// <summary>
/// Port of shared/JvCryption.cpp - KO's home-grown packet "encryption".
/// It is a symmetric XOR stream (encrypt == decrypt) and must stay
/// byte-identical with the 1453 client.
///
/// Version 1453 takes the #else branch of the original key selection:
///   #if __VERSION >= 1700            -> 0x1207500120128966
///   #elif 1298 &lt;= __VERSION &lt; 1453 -> 0x1234567890123456
///   #else (incl. 1453)               -> 0x7412580096385200
/// The original file also had a commented-out alternative marked "//1453":
/// 0x1257091582190465. <see cref="PrivateKey"/> uses the value the code as
/// shipped actually compiles with; if a real 1453 client fails the crypto
/// handshake, this constant is the first (and basically only) suspect.
/// </summary>
public sealed class JvCryption
{
    public const ulong PrivateKey = 0x7412580096385200UL; // see summary

    private ulong _publicKey;
    private ulong _sessionKey; // m_tkey = public ^ private

    public ulong PublicKey => _publicKey;

    /// <summary>Port of CJvCryption::GenerateKey() - never returns 0 (0 would disable encryption).</summary>
    public ulong GenerateKey()
    {
        Span<byte> bytes = stackalloc byte[8];
        do
        {
            RandomNumberGenerator.Fill(bytes);
            _publicKey = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        } while (_publicKey == 0);

        return _publicKey;
    }

    /// <summary>Sets the public key (used by tests / by the side that received the key).</summary>
    public void SetPublicKey(ulong publicKey) => _publicKey = publicKey;

    /// <summary>Port of CJvCryption::Init().</summary>
    public void Init() => _sessionKey = _publicKey ^ PrivateKey;

    /// <summary>Port of JvEncryptionFast(). Encrypts/decrypts in place (symmetric).</summary>
    public void EncryptFast(Span<byte> data) => EncryptFast((ReadOnlySpan<byte>)data, data);

    /// <summary>Port of JvDecryptionFast() - identical to <see cref="EncryptFast(Span{byte})"/>.</summary>
    public void DecryptFast(Span<byte> data) => EncryptFast(data);

    public void EncryptFast(ReadOnlySpan<byte> input, Span<byte> output)
    {
        // pkey is the raw little-endian memory image of the 64-bit session key.
        Span<byte> pkey = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(pkey, _sessionKey);

        var len = input.Length;
        var lkey = (byte)((len * 157) & 0xFF);
        var rkey = 2157;

        for (var i = 0; i < len; i++)
        {
            var rsk = (byte)((rkey >> 8) & 0xFF);
            output[i] = (byte)(((input[i] ^ rsk) ^ pkey[i % 8]) ^ lkey);
            rkey = unchecked(rkey * 2171); // int32 wraparound, as in the original
        }
    }

    /// <summary>
    /// Port of JvDecryptionWithCRC32(): decrypts in place, then verifies that
    /// the trailing 4 bytes are crc32(all preceding bytes, start = -1).
    /// Returns the plaintext length (input length - 4), or -1 on mismatch.
    /// </summary>
    public int DecryptWithCrc32(Span<byte> data)
    {
        if (data.Length < 4)
            return -1;

        DecryptFast(data);

        var plainLength = data.Length - 4;
        var expected = BinaryPrimitives.ReadUInt32LittleEndian(data[plainLength..]);
        var actual = KoCrc32.Compute(data[..plainLength], 0xFFFFFFFFu);
        return actual == expected ? plainLength : -1;
    }
}
