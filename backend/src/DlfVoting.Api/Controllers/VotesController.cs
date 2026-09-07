using System.Security.Claims;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api.Controllers;

[ApiController]
[Route("api/votes")]
public class VotesController : ControllerBase
{
    private const int PageSize = 25;

    private readonly DlfVotingDbContext _db;

    public VotesController(DlfVotingDbContext db)
    {
        _db = db;
    }

    public record CastVoteRequest(Guid VotingOptionId);
    public record MyVoteResponse(bool HasVoted, Guid? VotingOptionId, string? VotingOptionName, DateTime? UpdatedAt);
    public record AdminVoteResponse(Guid UserId, string Email, Guid? VotingOptionId, string? VotingOptionName, DateTime? UpdatedAt);
    public record PagedVotesResponse(List<AdminVoteResponse> Items, int TotalCount, int Page, int PageSize);

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // --- User-facing endpoints ---

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = AuthSchemes.User)]
    public async Task<IActionResult> GetMyVote()
    {
        var userId = GetUserId();

        var vote = await _db.Votes
            .Where(v => v.UserId == userId)
            .Join(_db.VotingOptions, v => v.VotingOptionId, o => o.Id,
                (v, o) => new MyVoteResponse(true, o.Id, o.Name, v.UpdatedAt))
            .FirstOrDefaultAsync();

        return Ok(vote ?? new MyVoteResponse(false, null, null, null));
    }

    [HttpPost]
    [Authorize(AuthenticationSchemes = AuthSchemes.User)]
    public async Task<IActionResult> CastVote([FromBody] CastVoteRequest request)
    {
        var userId = GetUserId();
        return await UpsertVoteAsync(userId, request.VotingOptionId);
    }

    // --- Admin-facing endpoints ---

    [HttpGet]
    [Authorize(AuthenticationSchemes = AuthSchemes.Admin)]
    public async Task<IActionResult> GetAllPaged([FromQuery] int page = 1, [FromQuery] bool onlyVoted = false)
    {
        if (page < 1) page = 1;

        IQueryable<AdminVoteResponse> query;

        if (onlyVoted)
        {
            query =
                from v in _db.Votes
                join u in _db.Users on v.UserId equals u.Id
                join o in _db.VotingOptions on v.VotingOptionId equals o.Id
                orderby u.Email
                select new AdminVoteResponse(u.Id, u.Email, (Guid?)o.Id, o.Name, (DateTime?)v.UpdatedAt);
        }
        else
        {
            var voteInfo =
                from v in _db.Votes
                join o in _db.VotingOptions on v.VotingOptionId equals o.Id
                select new { v.UserId, OptionId = o.Id, OptionName = o.Name, v.UpdatedAt };

            query =
                from u in _db.Users
                join vi in voteInfo on u.Id equals vi.UserId into viJoin
                from vi in viJoin.DefaultIfEmpty()
                orderby u.Email
                select new AdminVoteResponse(
                    u.Id,
                    u.Email,
                    vi != null ? (Guid?)vi.OptionId : null,
                    vi != null ? vi.OptionName : null,
                    vi != null ? (DateTime?)vi.UpdatedAt : null
                );
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        return Ok(new PagedVotesResponse(items, totalCount, page, PageSize));
    }

    [HttpPut("{userId}")]
    [Authorize(AuthenticationSchemes = AuthSchemes.Admin)]
    public async Task<IActionResult> AdminSetVote(Guid userId, [FromBody] CastVoteRequest request)
    {
        var userExists = await _db.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
        {
            return NotFound(new { message = "This user no longer exists." });
        }

        return await UpsertVoteAsync(userId, request.VotingOptionId);
    }

    [HttpDelete("{userId}")]
    [Authorize(AuthenticationSchemes = AuthSchemes.Admin)]
    public async Task<IActionResult> AdminDeleteVote(Guid userId)
    {
        var vote = await _db.Votes.FirstOrDefaultAsync(v => v.UserId == userId);
        if (vote is null)
        {
            return NotFound(new { message = "This user has not voted, or their vote was already removed." });
        }

        _db.Votes.Remove(vote);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return NotFound(new { message = "This user has not voted, or their vote was already removed." });
        }

        return NoContent();
    }

    // --- Shared upsert logic ---

    private async Task<IActionResult> UpsertVoteAsync(Guid userId, Guid votingOptionId)
    {
        var option = await _db.VotingOptions.FindAsync(votingOptionId);
        if (option is null)
        {
            return NotFound(new { message = "This voting option no longer exists." });
        }

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
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict(new { message = "Please try again." });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Please try again." });
        }

        return Ok(new MyVoteResponse(true, option.Id, option.Name, DateTime.UtcNow));
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is Npgsql.PostgresException { SqlState: "23505" };
    }
}