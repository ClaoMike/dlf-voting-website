namespace DlfVoting.Domain;

public class VotingSettings
{
    public Guid Id { get; set; }
    public bool IsVotingOpen { get; set; }
    public DateTime UpdatedAt { get; set; }
}