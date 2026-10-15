using System.Security.Cryptography;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary>Talabaga beriladigan vaqtinchalik parol: o'qilishi oson (o'xshash <c>0/O/o, 1/l/I/i</c> belgilarsiz), kamida bitta
/// kichik harf, katta harf va raqam — admin panel generatori (<c>generatePassword.ts</c>) bilan bir xil alifbo.
/// Kriptografik tasodif (<see cref="RandomNumberGenerator"/>).</summary>
public static class TemporaryPasswords
{
    public const int Length = 10;

    private const string Lower = "abcdefghjkmnpqrstuvwxyz";
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";
    private const string Alphabet = Lower + Upper + Digits;

    public static string Generate()
    {
        var chars = new char[Length];
        chars[0] = Pick(Lower);
        chars[1] = Pick(Upper);
        chars[2] = Pick(Digits);
        for (var i = 3; i < Length; i++)
            chars[i] = Pick(Alphabet);

        // Fisher–Yates: majburiy belgilar doim boshida turmasin.
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    private static char Pick(string chars) => chars[RandomNumberGenerator.GetInt32(chars.Length)];
}
