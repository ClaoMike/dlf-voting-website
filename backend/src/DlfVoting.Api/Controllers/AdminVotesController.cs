using DlfVoting.Api.Common;
using DlfVoting.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DlfVoting.Api.Controllers;

/// <summary>The admin overview: who has voted, resetting a user's vote, and the totals. Admins never see an individual vote.</summary>
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

    /// <summary>Resets a user's vote (deletes it), so they can vote again while voting is open.</summary>
    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> ResetVote(Guid userId) =>
        this.ToActionResult(await _votes.ResetAsync(userId));
}
