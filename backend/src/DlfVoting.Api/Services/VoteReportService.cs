using DlfVoting.Api.Contracts;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api.Services;

/// <summary>The admin overview: who voted for what, and the totals per option.</summary>
public class VoteReportService
{
    public const int PageSize = 25;

    private readonly DlfVotingDbContext _db;

    // ReSharper disable once ConvertToPrimaryConstructor
    public VoteReportService(DlfVotingDbContext db)
    {
        _db = db;
    }

    /// <summary>A page of users sorted by name, each with their vote (if any).</summary>
    public async Task<PagedVotesResponse> GetPageAsync(int page, bool onlyVoted)
    {
        if (page < 1) page = 1;

        var users = onlyVoted
            ? _db.Users.Where(u => _db.Votes.Any(v => v.UserId == u.Id))
            : _db.Users;

        var totalCount = await users.CountAsync();

        var pageUsers = await users
            .OrderByName()
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(u => new { u.Id, u.Username, u.FirstName, u.LastName })
            .ToListAsync();

        var userIds = pageUsers.Select(u => u.Id).ToList();
        var votes = await (
            from v in _db.Votes
            where userIds.Contains(v.UserId)
            join o in _db.VotingOptions on v.VotingOptionId equals o.Id
            select new { v.UserId, OptionId = o.Id, OptionName = o.Name, v.UpdatedAt }
        ).ToDictionaryAsync(v => v.UserId);

        var items = pageUsers
            .Select(u => votes.TryGetValue(u.Id, out var vote)
                ? new AdminVoteResponse(u.Id, u.Username, u.FirstName, u.LastName, vote.OptionId, vote.OptionName, vote.UpdatedAt)
                : new AdminVoteResponse(u.Id, u.Username, u.FirstName, u.LastName, null, null, null))
            .ToList();

        return new PagedVotesResponse(items, totalCount, page, PageSize);
    }

    public async Task<VoteStatsResponse> GetStatsAsync()
    {
        var totalUsers = await _db.Users.CountAsync();
        var votedUsers = await _db.Votes.CountAsync();

        var optionCounts = await (
            from o in _db.VotingOptions
            select new OptionVoteCount(o.Id, o.Name, _db.Votes.Count(v => v.VotingOptionId == o.Id))
        ).ToListAsync();

        return new VoteStatsResponse(totalUsers, votedUsers, optionCounts.OrderByDescending(o => o.Count).ToList());
    }
}
