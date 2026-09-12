namespace DlfVoting.Domain;

public class VotingSettings
{
    public Guid Id { get; init; }
    public bool IsVotingOpen { get; init; }
    public DateTime UpdatedAt { get; init; }
}