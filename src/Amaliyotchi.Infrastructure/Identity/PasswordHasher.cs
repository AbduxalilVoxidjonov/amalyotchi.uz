using System.Security.Cryptography;
using Amaliyotchi.Application.Common.Interfaces;

namespace Amaliyotchi.Infrastructure.Identity;

/// <summary>PBKDF2-HMAC-SHA256. Tashqi paketsiz, .NET kriptografiyasining o'zida.
/// Format: v1.{iteratsiya}.{tuz(base64)}.{xesh(base64)}</summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int CurrentIterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const string Prefix = "v1";

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt, CurrentIterations);

        return string.Join('.', Prefix, CurrentIterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string hash, out bool needsRehash)
    {
        needsRehash = false;

        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
            return false;

        var parts = hash.Split('.');
        if (parts.Length != 4 || parts[0] != Prefix)
            return false;

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
            return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Derive(password, salt, iterations);

        // Doimiy vaqtli taqqoslash — vaqt bo'yicha hujumning oldini oladi.
        var matches = CryptographicOperations.FixedTimeEquals(actual, expected);
        needsRehash = matches && iterations < CurrentIterations;

        return matches;
    }

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashSize);
}
