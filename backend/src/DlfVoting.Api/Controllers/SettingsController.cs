using DlfVoting.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api.Controllers;

[ApiController]
[Route("api/settings/voting")]
public class SettingsController : ControllerBase
{
    private readonly DlfVotingDbContext _db;

    public SettingsController(DlfVotingDbContext db)
    {
        _db = db;
    }

    public record VotingStatusResponse(bool IsVotingOpen);
    public record UpdateVotingStatusRequest(bool IsVotingOpen);

    [HttpGet]
    [Authorize(AuthenticationSchemes = $"{AuthSchemes.Admin},{AuthSchemes.User}")]
    public async Task<IActionResult> GetStatus()
    {
        var settings = await _db.VotingSettings.AsNoTracking().FirstOrDefaultAsync();
        return Ok(new VotingStatusResponse(settings?.IsVotingOpen ?? true));
    }

    [HttpPut]
    [Authorize(AuthenticationSchemes = AuthSchemes.Admin)]
    public async Task<IActionResult> UpdateStatus([FromBody] UpdateVotingStatusRequest request)
    {
        var settings = await _db.VotingSettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new Domain.VotingSettings { Id = Guid.NewGuid() };
            _db.VotingSettings.Add(settings);
        }

        settings.IsVotingOpen = request.IsVotingOpen;
        settings.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new VotingStatusResponse(settings.IsVotingOpen));
    }
}