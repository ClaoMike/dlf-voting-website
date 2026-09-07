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
    private static readonly Guid SettingsRowId = Guid.Parse("00000000-0000-0000-0000-000000000001");

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
        var settings = await _db.VotingSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == SettingsRowId);

        return Ok(new VotingStatusResponse(settings?.IsVotingOpen ?? true));
    }

    [HttpPut]
    [Authorize(AuthenticationSchemes = AuthSchemes.Admin)]
    public async Task<IActionResult> UpdateStatus([FromBody] UpdateVotingStatusRequest request)
    {
        var now = DateTime.UtcNow;

        await _db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"VotingSettings\" (\"Id\", \"IsVotingOpen\", \"UpdatedAt\") " +
            "VALUES ({0}, {1}, {2}) " +
            "ON CONFLICT (\"Id\") DO UPDATE SET \"IsVotingOpen\" = EXCLUDED.\"IsVotingOpen\", \"UpdatedAt\" = EXCLUDED.\"UpdatedAt\"",
            SettingsRowId, request.IsVotingOpen, now);

        return Ok(new VotingStatusResponse(request.IsVotingOpen));
    }
}