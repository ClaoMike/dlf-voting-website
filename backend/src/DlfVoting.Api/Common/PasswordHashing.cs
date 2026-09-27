namespace DlfVoting.Api.Common;

public static class PasswordHashing
{
    public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    // BCrypt is deliberately slow; hashing in parallel keeps large imports from taking minutes.
    public static List<string> HashMany(IEnumerable<string> passwords) =>
        passwords.AsParallel().AsOrdered().Select(Hash).ToList();
}
