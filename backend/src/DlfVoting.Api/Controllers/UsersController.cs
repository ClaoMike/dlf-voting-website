using System.Text.RegularExpressions;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// ReSharper disable NotAccessedPositionalProperty.Global
// ReSharper disable NotAccessedPositionalProperty.Local

namespace DlfVoting.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public partial class UsersController : ControllerBase
{
    private const int PageSize = 25;

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{20,64}$")]
    private static partial Regex PasswordRegex();

    private readonly DlfVotingDbContext _db;
    
    // ReSharper disable once ConvertToPrimaryConstructor
    public UsersController(DlfVotingDbContext db)
    {
        _db = db;
    }

    public record CreateUserRequest(string Email, string Password);
    public record UpdateUserRequest(string? Email, string? Password);
    public record UserResponse(Guid Id, string Email, DateTime CreatedAt);
    private record PagedUsersResponse(List<UserResponse> Items, int TotalCount, int Page,
        // ReSharper disable once MemberHidesStaticFromOuterClass
        int PageSize);
    public record BulkImportedUser(string Email, string Password);
    public record BulkImportSkippedEntry(string Email, string Reason);
    private record BulkImportResponse(List<BulkImportedUser> Created, List<BulkImportSkippedEntry> Skipped);

    [HttpPost("bulk-import")]
    public async Task<IActionResult> BulkImport(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Please provide a CSV file." });
        }

        var candidateEmails = new List<string>();
        using (var reader = new StreamReader(file.OpenReadStream()))
        {
            var isFirstLine = true;
            while (await reader.ReadLineAsync() is { } line)
            {
                var value = line.Split(',')[0].Trim().Trim('"');
                var isHeaderRow = isFirstLine && value.Equals("email", StringComparison.OrdinalIgnoreCase);
                isFirstLine = false;

                if (isHeaderRow || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                candidateEmails.Add(value);
            }
        }

        var skipped = new List<BulkImportSkippedEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidatesToCreate = new List<string>();

        foreach (var email in candidateEmails)
        {
            if (!EmailRegex().IsMatch(email))
            {
                skipped.Add(new BulkImportSkippedEntry(email, "Invalid email format"));
                continue;
            }

            if (!seen.Add(email))
            {
                skipped.Add(new BulkImportSkippedEntry(email, "Duplicate in file"));
                continue;
            }

            candidatesToCreate.Add(email);
        }

        var emailsToCreate = candidatesToCreate;

        if (candidatesToCreate.Count > 0)
        {
            var existingEmails = await _db.Users
                .Where(u => candidatesToCreate.Contains(u.Email))
                .Select(u => u.Email)
                .ToListAsync();

            var existingSet = new HashSet<string>(existingEmails, StringComparer.OrdinalIgnoreCase);
            
            skipped.AddRange(existingEmails.Select(email => new BulkImportSkippedEntry(email, "Already exists")));

            emailsToCreate = candidatesToCreate.Where(e => !existingSet.Contains(e)).ToList();
        }

        var created = new List<BulkImportedUser>();
        foreach (var email in emailsToCreate)
        {
            var password = SecurePasswordGenerator.Generate();

            _db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                CreatedAt = DateTime.UtcNow
            });

            created.Add(new BulkImportedUser(email, password));
        }

        if (created.Count == 0)
        {
            return Ok(new BulkImportResponse(created, skipped));
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return Conflict(new
            {
                message = "One or more emails were created by someone else at the same moment. Please re-upload the file to retry the remaining entries."
            });
        }

        return Ok(new BulkImportResponse(created, skipped));
    }

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
        var email = request.Email.Trim();

        if (!EmailRegex().IsMatch(email))
        {
            return BadRequest(new { message = "Please provide a valid email address." });
        }

        if (string.IsNullOrEmpty(request.Password) || !PasswordRegex().IsMatch(request.Password))
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

    [HttpPut("{id:guid}")]
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
            if (!EmailRegex().IsMatch(email))
            {
                return BadRequest(new { message = "Please provide a valid email address." });
            }
        }

        if (hasPassword && !PasswordRegex().IsMatch(request.Password!))
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

    [HttpDelete("{id:guid}")]
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