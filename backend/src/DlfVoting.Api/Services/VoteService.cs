using DlfVoting.Api.Common;
using DlfVoting.Api.Contracts;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DlfVoting.Api.Services;

/// <summary>Reading, casting and removing a single user's vote (used by both the user and the admin endpoints).</summary>
public class VoteService
{
    private const string NoVoteMessage = "This user has not voted, or their vote was already removed.";
    private const string OptionGoneMessage = "This voting option no longer exists.";
    private const string UserGoneMessage = "This user no longer exists.";

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

    /// <summary>
    /// Casts the user's vote, or changes it if they already voted (one vote row per user).
    /// With <paramref name="onlyWhileVotingOpen"/> the vote is refused once voting has been closed.
    /// </summary>
    public async Task<OperationResult<MyVoteResponse>> CastOrChangeAsync(Guid userId, Guid votingOptionId, bool onlyWhileVotingOpen)
    {
        var option = await _db.VotingOptions.AsNoTracking().FirstOrDefaultAsync(o => o.Id == votingOptionId);
        if (option is null) return OperationResult.NotFound(OptionGoneMessage);

        var now = DateTime.UtcNow;
        int written;
        try
        {
            // A single statement, so simultaneous requests for the same user can't collide: the unique index on UserId
            // turns the second insert into an update. The settings row is read FOR SHARE: closing voting (an update of
            // that row) waits for votes already being written, and votes queued behind a close re-read the row and see
            // it closed, so no vote lands after the admin has closed voting.
            written = await _db.Database.ExecuteSqlAsync($"""
                INSERT INTO "Votes" ("Id", "UserId", "VotingOptionId", "UpdatedAt")
                SELECT {Guid.NewGuid()}, {userId}, {option.Id}, {now}
                WHERE NOT {onlyWhileVotingOpen}
                   OR COALESCE((SELECT "IsVotingOpen" FROM "VotingSettings" LIMIT 1 FOR SHARE), TRUE)
                ON CONFLICT ("UserId") DO UPDATE
                SET "VotingOptionId" = EXCLUDED."VotingOptionId", "UpdatedAt" = EXCLUDED."UpdatedAt"
                """);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            // The user or the option was deleted between the checks above and the write.
            return OperationResult.NotFound(ex.ConstraintName == "FK_Votes_Users_UserId" ? UserGoneMessage : OptionGoneMessage);
        }

        return written == 0
            ? OperationResult.Forbidden(RequireVotingOpenAttribute.VotingClosedMessage)
            : OperationResult<MyVoteResponse>.Success(new MyVoteResponse(true, option.Id, option.Name, now));
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
