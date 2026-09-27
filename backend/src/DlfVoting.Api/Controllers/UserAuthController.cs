using System.Security.Claims;
using DlfVoting.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api.Controllers;

[ApiController]
[Route("api/auth/user")]
public class UserAuthController : ControllerBase
{
    private readonly DlfVotingDbContext _db;
    
    // ReSharper disable once ConvertToPrimaryConstructor
    public UserAuthController(DlfVotingDbContext db)
    {
        _db = db;
    }

    public record LoginRequest(string Username, string Password);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username.Trim());

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username)
        };

        if (user.Email is not null)
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        if (user.FirstName is not null)
        {
            claims.Add(new Claim(ClaimTypes.GivenName, user.FirstName));
        }

        if (user.LastName is not null)
        {
            claims.Add(new Claim(ClaimTypes.Surname, user.LastName));
        }

        var identity = new ClaimsIdentity(claims, AuthSchemes.User);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(AuthSchemes.User, principal, new AuthenticationProperties
        {
            // Expires after SessionValidation.Lifetime without activity; a browser session cookie otherwise.
            IsPersistent = false
        });

        return Ok(new { username = user.Username, email = user.Email, firstName = user.FirstName, lastName = user.LastName });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthSchemes.User);
        return Ok();
    }

    /// <summary>
    /// Keeps the session alive while the person is active in the browser without calling the API (e.g. reading the
    /// page). Any authenticated request renews the session; this one exists only to do that.
    /// </summary>
    [Authorize(AuthenticationSchemes = AuthSchemes.User)]
    [HttpPost("refresh")]
    public IActionResult Refresh() => NoContent();

    [Authorize(AuthenticationSchemes = AuthSchemes.User)]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            username = User.FindFirstValue(ClaimTypes.Name),
            email = User.FindFirstValue(ClaimTypes.Email),
            firstName = User.FindFirstValue(ClaimTypes.GivenName),
            lastName = User.FindFirstValue(ClaimTypes.Surname)
        });
    }
}