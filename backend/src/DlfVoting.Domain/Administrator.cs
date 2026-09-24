namespace DlfVoting.Domain;

public class Administrator
{
    // Admin usernames were backfilled from their emails, so they allow email-length values.
    public const int UsernameMaxLength = 320;

    public Guid Id { get; init; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}