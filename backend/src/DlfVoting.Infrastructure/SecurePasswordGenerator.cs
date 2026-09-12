using System.Security.Cryptography;

namespace DlfVoting.Infrastructure;

public static class SecurePasswordGenerator
{
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijkmnpqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Special = "!@#$%^&*_-+=?";
    private const string All = Uppercase + Lowercase + Digits + Special;

    public static string Generate(int length = 24)
    {
        var required = new[]
        {
            RandomChar(Uppercase),
            RandomChar(Digits),
            RandomChar(Special)
        };

        var remaining = new char[Math.Max(length - required.Length, 0)];
        for (var i = 0; i < remaining.Length; i++)
        {
            remaining[i] = RandomChar(All);
        }

        var all = required.Concat(remaining).ToArray();
        Shuffle(all);
        return new string(all);
    }

    private static char RandomChar(string charset)
    {
        var index = RandomNumberGenerator.GetInt32(charset.Length);
        return charset[index];
    }

    private static void Shuffle(char[] array)
    {
        for (var i = array.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }
}