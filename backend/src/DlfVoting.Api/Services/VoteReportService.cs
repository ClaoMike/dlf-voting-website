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

    /// <summary>
    /// The full results report: totals, votes per option, and who did and didn't vote (never what anyone voted for).
    /// Read-only: it runs in a READ ONLY transaction, so it cannot change a vote even by mistake.
    /// </summary>
    /// <param name="adminId">The admin asking for it; their current username is printed on the report.</param>
    /// <param name="fallbackUsername">Used if that admin can't be found (their session's username).</param>
    public async Task<VoteReportResponse> GetReportAsync(Guid adminId, string fallbackUsername)
    {
        // One snapshot, so the totals, the per-option counts and the two lists all describe the same moment.
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await _db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");

        var generatedBy = await _db.Administrators.Where(a => a.Id == adminId).Select(a => a.Username).FirstOrDefaultAsync()
                          ?? fallbackUsername;

        // Same default as the voting status endpoint: no settings row means voting is open.
        var isVotingOpen = await _db.VotingSettings.Select(s => (bool?)s.IsVotingOpen).FirstOrDefaultAsync() ?? true;

        var optionCounts = await (
            from o in _db.VotingOptions
            select new OptionVoteCount(o.Id, o.Name, _db.Votes.Count(v => v.VotingOptionId == o.Id))
        ).ToListAsync();

        var users = await _db.Users
            .OrderByName()
            .Select(u => new
            {
                u.FirstName,
                u.LastName,
                u.Username,
                u.EmployeeCode,
                u.CompanyCode,
                HasVoted = _db.Votes.Any(v => v.UserId == u.Id),
            })
            .ToListAsync();

        var voted = new List<ReportUser>();
        var notVoted = new List<ReportUser>();
        foreach (var u in users)
        {
            var row = new ReportUser(UserQueryExtensions.FullName(u.FirstName, u.LastName), u.Username, u.EmployeeCode, u.CompanyCode);
            (u.HasVoted ? voted : notVoted).Add(row);
        }

        await transaction.CommitAsync();

        return new VoteReportResponse(
            DateTime.UtcNow,
            generatedBy,
            isVotingOpen,
            users.Count,
            voted.Count,
            optionCounts.OrderByDescending(o => o.Count).ThenBy(o => o.VotingOptionName).ToList(),
            voted,
            notVoted);
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
