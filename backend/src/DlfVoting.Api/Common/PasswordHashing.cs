namespace DlfVoting.Api.Common;

public static class PasswordHashing
{
    public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    /// <summary>
    /// BCrypt is deliberately slow (about 0.7 s per password on an Azure B1 core), so hash in parallel and report
    /// each finished password through <paramref name="onHashed"/> (imports show it as progress).
    /// </summary>
    public static List<string> HashMany(IReadOnlyList<string> passwords, Action? onHashed = null)
    {
        var hashes = new string[passwords.Count];
        Parallel.For(0, passwords.Count, i =>
        {
            hashes[i] = Hash(passwords[i]);
            onHashed?.Invoke();
        });
        return [.. hashes];
    }
}
