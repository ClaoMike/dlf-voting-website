// ReSharper disable NotAccessedPositionalProperty.Global

namespace DlfVoting.Api.Contracts;

public record CastVoteRequest(Guid VotingOptionId);

public record MyVoteResponse(bool HasVoted, Guid? VotingOptionId, string? VotingOptionName, DateTime? UpdatedAt)
{
    public static readonly MyVoteResponse NotVoted = new(false, null, null, null);
}

/// <summary>A row of the admin overview: whether the user voted, never what they voted for.</summary>
public record AdminVoteResponse(Guid UserId, string Username, string? FirstName, string? LastName, bool HasVoted);

public record PagedVotesResponse(List<AdminVoteResponse> Items, int TotalCount, int Page, int PageSize);

public record OptionVoteCount(Guid VotingOptionId, string VotingOptionName, int Count);

public record VoteStatsResponse(int TotalUsers, int VotedUsers, List<OptionVoteCount> OptionCounts);

/// <summary>A person in the results report. Like the overview, it says whether they voted, never what for.</summary>
public record ReportUser(string? Name, string Username, string? EmployeeCode, string? CompanyCode);

/// <summary>Everything the printable results report can show, read at one moment.</summary>
public record VoteReportResponse(
    DateTime GeneratedAt,
    string GeneratedBy,
    bool IsVotingOpen,
    int TotalUsers,
    int VotedUsers,
    List<OptionVoteCount> OptionCounts,
    List<ReportUser> UsersWhoVoted,
    List<ReportUser> UsersWhoDidNotVote);
