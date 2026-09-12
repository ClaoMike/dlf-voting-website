namespace DlfVoting.Domain;

public class Vote
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public Guid VotingOptionId { get; set; }
    public DateTime UpdatedAt { get; set; }
}