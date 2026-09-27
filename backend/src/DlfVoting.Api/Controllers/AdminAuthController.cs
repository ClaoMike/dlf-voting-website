using System.Security.Claims;
using DlfVoting.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api.Controllers;

[ApiController]
[Route("api/auth/admin")]
public class AdminAuthController : ControllerBase
{
    private readonly DlfVotingDbContext _db;
    private readonly LoginAttemptLimiter _loginAttempts;

    // ReSharper disable once ConvertToPrimaryConstructor
    public AdminAuthController(DlfVotingDbContext db, LoginAttemptLimiter loginAttempts)
    {
        _db = db;
        _loginAttempts = loginAttempts;
    }

    public record LoginRequest(string Username, string Password);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!_loginAttempts.TryAttempt(AuthSchemes.Admin, request.Username))
        {
            Response.Headers.RetryAfter = "300";
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = LoginAttemptLimiter.TooManyAttemptsMessage });
        }

        var admin = await _db.Administrators
            .FirstOrDefaultAsync(a => a.Username == request.Username.Trim());

        if (admin is null || !BCrypt.Net.BCrypt.Verify(request.Password, admin.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, admin.Id.ToString()),
            new(ClaimTypes.Name, admin.Username),
            new(ClaimTypes.Email, admin.Email)
        };

        var identity = new ClaimsIdentity(claims, AuthSchemes.Admin);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(AuthSchemes.Admin, principal, new AuthenticationProperties
        {
            // Expires after SessionValidation.Lifetime without activity; a browser session cookie otherwise.
            IsPersistent = false
        });

        return Ok(new { username = admin.Username, email = admin.Email });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthSchemes.Admin);
        return Ok();
    }

    /// <summary>
    /// Keeps the session alive while the person is active in the browser without calling the API (e.g. reading the
    /// page). Any authenticated request renews the session; this one exists only to do that.
    /// </summary>
    [Authorize(AuthenticationSchemes = AuthSchemes.Admin)]
    [HttpPost("refresh")]
    public IActionResult Refresh() => NoContent();

    [Authorize(AuthenticationSchemes = AuthSchemes.Admin)]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            username = User.FindFirstValue(ClaimTypes.Name),
            email = User.FindFirstValue(ClaimTypes.Email)
        });
    }
}