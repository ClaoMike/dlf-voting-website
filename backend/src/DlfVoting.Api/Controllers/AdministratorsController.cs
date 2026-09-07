using System.Security.Claims;
using System.Text.RegularExpressions;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api.Controllers;

[ApiController]
[Route("api/administrators")]
[Authorize(AuthenticationSchemes = AuthSchemes.Admin)]
public class AdministratorsController : ControllerBase
{
    private const int PageSize = 25;

    private static readonly Regex EmailRegex = new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled);
    private static readonly Regex PasswordRegex = new(
        @"^(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{20,64}$", RegexOptions.Compiled);

    private readonly DlfVotingDbContext _db;

    public AdministratorsController(DlfVotingDbContext db)
    {
        _db = db;
    }

    public record CreateAdministratorRequest(string Email, string Password);
    public record UpdateAdministratorRequest(string? Email, string? Password);
    public record AdministratorResponse(Guid Id, string Email, DateTime CreatedAt);
    public record PagedAdministratorsResponse(List<AdministratorResponse> Items, int TotalCount, int Page, int PageSize);

    private Guid GetCurrentAdminId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] int page = 1)
    {
        if (page < 1) page = 1;

        var currentAdminId = GetCurrentAdminId();

        var totalCount = await _db.Administrators.CountAsync();

        var items = await _db.Administrators
            .OrderBy(a => a.Id == currentAdminId ? 0 : 1)
            .ThenBy(a => a.Email)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(a => new AdministratorResponse(a.Id, a.Email, a.CreatedAt))
            .ToListAsync();

        return Ok(new PagedAdministratorsResponse(items, totalCount, page, PageSize));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAdministratorRequest request)
    {
        var email = request.Email?.Trim() ?? string.Empty;

        if (!EmailRegex.IsMatch(email))
        {
            return BadRequest(new { message = "Please provide a valid email address." });
        }

        if (string.IsNullOrEmpty(request.Password) || !PasswordRegex.IsMatch(request.Password))
        {
            return BadRequest(new
            {
                message = "Password must be 20-64 characters and include at least one uppercase letter, one digit, and one special character."
            });
        }

        var alreadyExists = await _db.Administrators.AnyAsync(a => a.Email == email);
        if (alreadyExists)
        {
            return Conflict(new { message = "An administrator with this email already exists." });
        }

        var admin = new Administrator
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        _db.Administrators.Add(admin);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict(new { message = "An administrator with this email already exists." });
        }

        return Ok(new AdministratorResponse(admin.Id, admin.Email, admin.CreatedAt));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAdministratorRequest request)
    {
        if (id == GetCurrentAdminId())
        {
            return StatusCode(403, new { message = "You cannot edit your own administrator account here." });
        }

        var hasEmail = !string.IsNullOrWhiteSpace(request.Email);
        var hasPassword = !string.IsNullOrEmpty(request.Password);

        if (!hasEmail && !hasPassword)
        {
            return BadRequest(new { message = "Provide a new email, a new password, or both." });
        }

        string? email = null;
        if (hasEmail)
        {
            email = request.Email!.Trim();
            if (!EmailRegex.IsMatch(email))
            {
                return BadRequest(new { message = "Please provide a valid email address." });
            }
        }

        if (hasPassword && !PasswordRegex.IsMatch(request.Password!))
        {
            return BadRequest(new
            {
                message = "Password must be 20-64 characters and include at least one uppercase letter, one digit, and one special character."
            });
        }

        var admin = await _db.Administrators.FindAsync(id);
        if (admin is null)
        {
            return NotFound(new { message = "This administrator no longer exists." });
        }

        if (hasEmail)
        {
            var emailTakenByAnother = await _db.Administrators.AnyAsync(a => a.Email == email && a.Id != id);
            if (emailTakenByAnother)
            {
                return Conflict(new { message = "An administrator with this email already exists." });
            }
            admin.Email = email!;
        }

        if (hasPassword)
        {
            admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password!);
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return NotFound(new { message = "This administrator no longer exists." });
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict(new { message = "An administrator with this email already exists." });
        }

        return Ok(new AdministratorResponse(admin.Id, admin.Email, admin.CreatedAt));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (id == GetCurrentAdminId())
        {
            return StatusCode(403, new { message = "You cannot remove your own administrator account." });
        }

        var admin = await _db.Administrators.FindAsync(id);
        if (admin is null)
        {
            return NotFound(new { message = "This administrator has already been removed." });
        }

        _db.Administrators.Remove(admin);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return NotFound(new { message = "This administrator has already been removed." });
        }

        return NoContent();
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is Npgsql.PostgresException { SqlState: "23505" };
    }
}