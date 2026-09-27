using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Infrastructure;

public static class UsernameGenerator
{
    // Lowercase letters and digits without look-alikes (0/o, 1/l/i), since people type these by hand.
    private const string Alphabet = "abcdefghjkmnpqrstuvwxyz23456789";
    private const int Length = 8;

    public static string Generate()
    {
        var chars = new char[Length];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return new string(chars);
    }

    /// <summary>
    /// Generates <paramref name="count"/> usernames that are distinct from each other and from every existing user.
    /// </summary>
    public static async Task<List<string>> GenerateUniqueAsync(DlfVotingDbContext db, int count)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (result.Count < count)
        {
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (candidates.Count < count - result.Count)
            {
                var candidate = Generate();
                if (!result.Contains(candidate))
                {
                    candidates.Add(candidate);
                }
            }

            var candidateList = candidates.ToList();
            var taken = await db.Users
                .Where(u => candidateList.Contains(u.Username))
                .Select(u => u.Username)
                .ToListAsync();

            candidates.ExceptWith(taken);
            result.UnionWith(candidates);
        }

        return result.ToList();
    }
}
