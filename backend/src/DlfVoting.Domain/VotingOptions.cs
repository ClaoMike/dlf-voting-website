namespace DlfVoting.Domain;

public class VotingOption
{
    public Guid Id { get; init; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}