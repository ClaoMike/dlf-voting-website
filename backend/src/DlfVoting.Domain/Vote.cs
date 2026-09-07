namespace DlfVoting.Domain;

public class Vote
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid VotingOptionId { get; set; }
    public DateTime UpdatedAt { get; set; }
}