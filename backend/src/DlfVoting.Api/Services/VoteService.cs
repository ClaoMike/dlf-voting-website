using DlfVoting.Api.Common;
using DlfVoting.Api.Contracts;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api.Services;

/// <summary>Reading, casting and removing a single user's vote (used by both the user and the admin endpoints).</summary>
public class VoteService
{
    private const string NoVoteMessage = "This user has not voted, or their vote was already removed.";

    private readonly DlfVotingDbContext _db;

    // ReSharper disable once ConvertToPrimaryConstructor
    public VoteService(DlfVotingDbContext db)
    {
        _db = db;
    }

    public async Task<MyVoteResponse> GetVoteAsync(Guid userId) =>
        await _db.Votes
            .Where(v => v.UserId == userId)
            .Join(_db.VotingOptions, v => v.VotingOptionId, o => o.Id,
                (v, o) => new MyVoteResponse(true, o.Id, o.Name, v.UpdatedAt))
            .FirstOrDefaultAsync()
        ?? MyVoteResponse.NotVoted;

    public Task<bool> UserExistsAsync(Guid userId) => _db.Users.AnyAsync(u => u.Id == userId);

    /// <summary>Casts the user's vote, or changes it if they already voted (one vote row per user).</summary>
    public async Task<OperationResult<MyVoteResponse>> CastOrChangeAsync(Guid userId, Guid votingOptionId)
    {
        var option = await _db.VotingOptions.FindAsync(votingOptionId);
        if (option is null) return OperationResult.NotFound("This voting option no longer exists.");

        var existingVote = await _db.Votes.FirstOrDefaultAsync(v => v.UserId == userId);
        if (existingVote is not null)
        {
            existingVote.VotingOptionId = option.Id;
            existingVote.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.Votes.Add(new Vote
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                VotingOptionId = option.Id,
                UpdatedAt = DateTime.UtcNow
            });
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation() || ex is DbUpdateConcurrencyException)
        {
            // Another request for the same user won the race; the client retries.
            return OperationResult.Conflict("Please try again.");
        }

        return OperationResult<MyVoteResponse>.Success(new MyVoteResponse(true, option.Id, option.Name, DateTime.UtcNow));
    }

    public async Task<OperationResult> DeleteAsync(Guid userId)
    {
        var vote = await _db.Votes.FirstOrDefaultAsync(v => v.UserId == userId);
        if (vote is null) return OperationResult.NotFound(NoVoteMessage);

        _db.Votes.Remove(vote);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.NotFound(NoVoteMessage);
        }

        return OperationResult.Success;
    }
}
