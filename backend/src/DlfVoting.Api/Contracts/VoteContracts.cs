// ReSharper disable NotAccessedPositionalProperty.Global

namespace DlfVoting.Api.Contracts;

public record CastVoteRequest(Guid VotingOptionId);

public record MyVoteResponse(bool HasVoted, Guid? VotingOptionId, string? VotingOptionName, DateTime? UpdatedAt)
{
    public static readonly MyVoteResponse NotVoted = new(false, null, null, null);
}

public record AdminVoteResponse(
    Guid UserId,
    string Username,
    string? FirstName,
    string? LastName,
    Guid? VotingOptionId,
    string? VotingOptionName,
    DateTime? UpdatedAt);

public record PagedVotesResponse(List<AdminVoteResponse> Items, int TotalCount, int Page, int PageSize);

public record OptionVoteCount(Guid VotingOptionId, string VotingOptionName, int Count);

public record VoteStatsResponse(int TotalUsers, int VotedUsers, List<OptionVoteCount> OptionCounts);
