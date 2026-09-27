using System.Security.Claims;
using DlfVoting.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api;

/// <summary>
/// Runs on every request that carries a session cookie. Cookie sessions are stateless, so without this a deleted admin
/// or user keeps working until their cookie expires (and a deleted admin could create themselves a new account in that
/// window): every request re-checks the account. A valid session is then renewed, so it only ends after
/// <see cref="Lifetime"/> without any activity.
/// </summary>
public static class SessionValidation
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public static Task ValidateAdminAsync(CookieValidatePrincipalContext context) =>
        RejectUnlessAccountExistsAsync(context, (db, id) => db.Administrators.AnyAsync(a => a.Id == id));

    public static Task ValidateUserAsync(CookieValidatePrincipalContext context) =>
        RejectUnlessAccountExistsAsync(context, (db, id) => db.Users.AnyAsync(u => u.Id == id));

    private static async Task RejectUnlessAccountExistsAsync(
        CookieValidatePrincipalContext context, Func<DlfVotingDbContext, Guid, Task<bool>> accountExists)
    {
        var db = context.HttpContext.RequestServices.GetRequiredService<DlfVotingDbContext>();
        if (Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && await accountExists(db, id))
        {
            // Re-issue the cookie with a fresh Lifetime on every request (the built-in sliding expiration only renews
            // after half the lifetime has passed). Not applied when the same response signs in or out.
            context.ShouldRenew = true;
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }
}
