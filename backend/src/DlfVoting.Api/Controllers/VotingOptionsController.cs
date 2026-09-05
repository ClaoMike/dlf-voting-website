using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api.Controllers;

[ApiController]
[Route("api/voting-options")]
[Authorize]
public class VotingOptionsController : ControllerBase
{
    private readonly DlfVotingDbContext _db;

    public VotingOptionsController(DlfVotingDbContext db)
    {
        _db = db;
    }

    public record CreateVotingOptionRequest(string Name);
    public record UpdateVotingOptionRequest(string Name);
    public record VotingOptionResponse(Guid Id, string Name, DateTime CreatedAt);

    // NOTE: this will require voter authentication too, once the Users feature exists.
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var options = await _db.VotingOptions
            .OrderBy(v => v.Name)
            .Select(v => new VotingOptionResponse(v.Id, v.Name, v.CreatedAt))
            .ToListAsync();

        return Ok(options);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVotingOptionRequest request)
    {
        var name = request.Name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "Name cannot be empty." });
        }

        var alreadyExists = await _db.VotingOptions
            .AnyAsync(v => v.Name == name);

        if (alreadyExists)
        {
            return Conflict(new { message = "A voting option with this name already exists." });
        }

        var option = new VotingOption
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = DateTime.UtcNow
        };

        _db.VotingOptions.Add(option);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict(new { message = "A voting option with this name already exists." });
        }

        return Ok(new VotingOptionResponse(option.Id, option.Name, option.CreatedAt));
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is Npgsql.PostgresException { SqlState: "23505" };
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVotingOptionRequest request)
    {
        var name = request.Name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "Name cannot be empty." });
        }

        var option = await _db.VotingOptions.FindAsync(id);
        if (option is null)
        {
            return NotFound(new { message = "This voting option no longer exists." });
        }

        var nameTakenByAnother = await _db.VotingOptions
            .AnyAsync(v => v.Name == name && v.Id != id);

        if (nameTakenByAnother)
        {
            return Conflict(new { message = "A voting option with this name already exists." });
        }

        option.Name = name;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Deleted by another request between our FindAsync and SaveChangesAsync
            return NotFound(new { message = "This voting option no longer exists." });
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict(new { message = "A voting option with this name already exists." });
        }

        return Ok(new VotingOptionResponse(option.Id, option.Name, option.CreatedAt));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var option = await _db.VotingOptions.FindAsync(id);
        if (option is null)
        {
            return NotFound(new { message = "This voting option has already been deleted." });
        }

        _db.VotingOptions.Remove(option);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request deleted it between our FindAsync and SaveChangesAsync
            return NotFound(new { message = "This voting option has already been deleted." });
        }

        return NoContent();
    }
    
}