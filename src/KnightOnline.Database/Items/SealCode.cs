using System.Security.Cryptography;
namespace KnightOnline.Database.Items;

/// <summary>Explicit server extension: no shared legacy 12345678 default is accepted.
/// Codes never belong in command-line arguments, chat, packet traces or ToString.</summary>
public static class SealCode
{
    public const int Iterations = 210000;
    public static bool Valid(string code) => code.Length == 8 && code.All(c => c is >= '0' and <= '9') && code != "12345678" && code.Distinct().Count() > 1;
    public static byte[] Hash(string code, byte[] salt) => Rfc2898DeriveBytes.Pbkdf2(code, salt, Iterations, HashAlgorithmName.SHA256, 32);
    public static bool Verify(string code, byte[] salt, byte[] hash) => salt.Length == 16 && hash.Length == 32 && CryptographicOperations.FixedTimeEquals(Hash(code, salt), hash);
}
