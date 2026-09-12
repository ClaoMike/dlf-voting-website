namespace DlfVoting.Domain;

public class Administrator
{
    public Guid Id { get; init; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}