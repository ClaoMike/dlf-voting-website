using System.Data;
using DlfVoting.Api.Common;
using DlfVoting.Api.Contracts;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api.Services;

/// <summary>The admin overview: who voted (but not for what), and the totals per option.</summary>
public class VoteReportService
{
    public const int PageSize = 25;

    private readonly DlfVotingDbContext _db;

    // ReSharper disable once ConvertToPrimaryConstructor
    public VoteReportService(DlfVotingDbContext db)
    {
        _db = db;
    }

    /// <summary>A page of users sorted by name, each with whether they voted (not what they voted for).</summary>
    public async Task<PagedVotesResponse> GetPageAsync(int page, bool onlyVoted)
    {
        page = Paging.ClampPage(page, PageSize);

        var users = onlyVoted
            ? _db.Users.Where(u => _db.Votes.Any(v => v.UserId == u.Id))
            : _db.Users;

        var totalCount = await users.CountAsync();

        var items = await users
            .OrderByName()
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(u => new AdminVoteResponse(u.Id, u.Username, u.FirstName, u.LastName, _db.Votes.Any(v => v.UserId == u.Id)))
            .ToListAsync();

        return new PagedVotesResponse(items, totalCount, page, PageSize);
    }

    public async Task<VoteStatsResponse> GetStatsAsync()
    {
        // One snapshot for all three queries, so totals and per-option counts agree while votes are coming in.
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        var totalUsers = await _db.Users.CountAsync();
        var votedUsers = await _db.Votes.CountAsync();

        var optionCounts = await (
            from o in _db.VotingOptions
            select new OptionVoteCount(o.Id, o.Name, _db.Votes.Count(v => v.VotingOptionId == o.Id))
        ).ToListAsync();

        return new VoteStatsResponse(totalUsers, votedUsers, optionCounts.OrderByDescending(o => o.Count).ToList());
    }
}
