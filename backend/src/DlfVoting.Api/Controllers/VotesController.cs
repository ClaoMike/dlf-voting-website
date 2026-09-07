using System.Security.Claims;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api.Controllers;

[ApiController]
[Route("api/votes")]
[Authorize(AuthenticationSchemes = AuthSchemes.User)]
public class VotesController : ControllerBase
{
    private readonly DlfVotingDbContext _db;

    public VotesController(DlfVotingDbContext db)
    {
        _db = db;
    }

    public record CastVoteRequest(Guid VotingOptionId);
    public record MyVoteResponse(bool HasVoted, Guid? VotingOptionId, string? VotingOptionName, DateTime? UpdatedAt);

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("me")]
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
    public async Task<IActionResult> CastVote([FromBody] CastVoteRequest request)
    {
        var userId = GetUserId();

        var option = await _db.VotingOptions.FindAsync(request.VotingOptionId);
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
            // Two simultaneous vote requests from the same user raced to insert their first vote.
            return Conflict(new { message = "Please try voting again." });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Please try voting again." });
        }

        return Ok(new MyVoteResponse(true, option.Id, option.Name, DateTime.UtcNow));
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is Npgsql.PostgresException { SqlState: "23505" };
    }
}