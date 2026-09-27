using DlfVoting.Api.Common;
using DlfVoting.Api.Contracts;
using DlfVoting.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DlfVoting.Api.Controllers;

/// <summary>The admin overview: listing everyone's votes, changing or removing a vote, and the totals.</summary>
[ApiController]
[Route("api/votes")]
[Authorize(AuthenticationSchemes = AuthSchemes.Admin)]
public class AdminVotesController : ControllerBase
{
    private readonly VoteService _votes;
    private readonly VoteReportService _reports;

    // ReSharper disable once ConvertToPrimaryConstructor
    public AdminVotesController(VoteService votes, VoteReportService reports)
    {
        _votes = votes;
        _reports = reports;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllPaged([FromQuery] int page = 1, [FromQuery] bool onlyVoted = false) =>
        Ok(await _reports.GetPageAsync(page, onlyVoted));

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats() =>
        Ok(await _reports.GetStatsAsync());

    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> SetVote(Guid userId, [FromBody] CastVoteRequest request)
    {
        if (!await _votes.UserExistsAsync(userId))
        {
            return NotFound(new { message = "This user no longer exists." });
        }

        return this.ToActionResult(await _votes.CastOrChangeAsync(userId, request.VotingOptionId, onlyWhileVotingOpen: false));
    }

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> DeleteVote(Guid userId) =>
        this.ToActionResult(await _votes.DeleteAsync(userId));
}
