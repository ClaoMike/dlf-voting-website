using System.Security.Claims;
using DlfVoting.Api.Common;
using DlfVoting.Api.Contracts;
using DlfVoting.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DlfVoting.Api.Controllers;

/// <summary>The signed-in user's own vote. Only available while voting is open.</summary>
[ApiController]
[Route("api/votes")]
[Authorize(AuthenticationSchemes = AuthSchemes.User)]
[RequireVotingOpen]
public class MyVoteController : ControllerBase
{
    private readonly VoteService _votes;

    // ReSharper disable once ConvertToPrimaryConstructor
    public MyVoteController(VoteService votes)
    {
        _votes = votes;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("me")]
    public async Task<IActionResult> GetMyVote() =>
        Ok(await _votes.GetVoteAsync(CurrentUserId));

    [HttpPost]
    public async Task<IActionResult> CastVote([FromBody] CastVoteRequest request) =>
        this.ToActionResult(await _votes.CastOrChangeAsync(CurrentUserId, request.VotingOptionId, onlyWhileVotingOpen: true));
}
