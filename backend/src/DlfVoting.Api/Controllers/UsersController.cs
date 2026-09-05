using System.Text.RegularExpressions;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private const int PageSize = 25;

    private static readonly Regex EmailRegex = new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled);
    private static readonly Regex PasswordRegex = new(
        @"^(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{20,64}$", RegexOptions.Compiled);

    private readonly DlfVotingDbContext _db;

    public UsersController(DlfVotingDbContext db)
    {
        _db = db;
    }

    public record CreateUserRequest(string Email, string Password);
    public record UpdateUserRequest(string? Email, string? Password);
    public record UserResponse(Guid Id, string Email, DateTime CreatedAt);
    public record PagedUsersResponse(List<UserResponse> Items, int TotalCount, int Page, int PageSize);

    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] int page = 1)
    {
        if (page < 1) page = 1;

        var totalCount = await _db.Users.CountAsync();

        var items = await _db.Users
            .OrderBy(u => u.Email)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(u => new UserResponse(u.Id, u.Email, u.CreatedAt))
            .ToListAsync();

        return Ok(new PagedUsersResponse(items, totalCount, page, PageSize));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
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

        var alreadyExists = await _db.Users.AnyAsync(u => u.Email == email);
        if (alreadyExists)
        {
            return Conflict(new { message = "A user with this email already exists." });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict(new { message = "A user with this email already exists." });
        }

        return Ok(new UserResponse(user.Id, user.Email, user.CreatedAt));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
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

        var user = await _db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound(new { message = "This user no longer exists." });
        }

        if (hasEmail)
        {
            var emailTakenByAnother = await _db.Users.AnyAsync(u => u.Email == email && u.Id != id);
            if (emailTakenByAnother)
            {
                return Conflict(new { message = "A user with this email already exists." });
            }
            user.Email = email!;
        }

        if (hasPassword)
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password!);
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return NotFound(new { message = "This user no longer exists." });
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict(new { message = "A user with this email already exists." });
        }

        return Ok(new UserResponse(user.Id, user.Email, user.CreatedAt));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound(new { message = "This user has already been deleted." });
        }

        _db.Users.Remove(user);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return NotFound(new { message = "This user has already been deleted." });
        }

        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAll()
    {
        await _db.Users.ExecuteDeleteAsync();
        return NoContent();
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is Npgsql.PostgresException { SqlState: "23505" };
    }
}