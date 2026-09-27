using System.Net;
using System.Net.Http.Json;
using System.Text;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DlfVoting.Api.Tests.Tests;

/// <summary>
/// Every admin-only endpoint, hit with no session, a voter's session, and forged cookies.
/// Several controllers use a bare [Authorize] and rely on the Admin scheme being the default,
/// so these pin that a voter's cookie never reaches them.
/// </summary>
public class AuthorizationMatrixTests : IntegrationTestBase
{
    private const string AdminCookieName = "DlfVotingAdminAuth";
    private const string UserCookieName = "DlfVotingUserAuth";
    private const string AttackerUsername = "attacker";
    private const string AttackerPassword = "AttackerPassword1234!@#";
    private const string NewPassword = "HijackedPassword1234!@#";

    // ReSharper disable once ConvertToPrimaryConstructor
    public AuthorizationMatrixTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    public static TheoryData<string, string> AdminOnlyEndpoints => new()
    {
        { "GET", "/api/auth/admin/me" },
        { "POST", "/api/auth/admin/refresh" },
        { "GET", "/api/users" },
        { "GET", $"/api/users/{Guid.NewGuid()}" },
        { "POST", "/api/users" },
        { "PUT", $"/api/users/{Guid.NewGuid()}" },
        { "DELETE", $"/api/users/{Guid.NewGuid()}" },
        { "DELETE", "/api/users" },
        { "POST", "/api/users/bulk-import" },
        { "POST", "/api/users/import-employees" },
        { "GET", $"/api/users/imports/{Guid.NewGuid()}" },
        { "DELETE", $"/api/users/imports/{Guid.NewGuid()}" },
        { "POST", "/api/voting-options" },
        { "PUT", $"/api/voting-options/{Guid.NewGuid()}" },
        { "DELETE", $"/api/voting-options/{Guid.NewGuid()}" },
        { "DELETE", "/api/voting-options" },
        { "GET", "/api/administrators" },
        { "POST", "/api/administrators" },
        { "PUT", $"/api/administrators/{Guid.NewGuid()}" },
        { "DELETE", $"/api/administrators/{Guid.NewGuid()}" },
        { "PUT", "/api/administrators/me/password" },
        { "GET", "/api/votes" },
        { "GET", "/api/votes/stats" },
        { "PUT", $"/api/votes/{Guid.NewGuid()}" },
        { "DELETE", $"/api/votes/{Guid.NewGuid()}" },
        { "PUT", "/api/settings/voting" },
    };

    public static TheoryData<string, string> UserOnlyEndpoints => new()
    {
        { "GET", "/api/auth/user/me" },
        { "POST", "/api/auth/user/refresh" },
        { "GET", "/api/votes/me" },
        { "POST", "/api/votes" },
    };

    private static HttpRequestMessage Request(string method, string url) =>
        new(new HttpMethod(method), url) { Content = BodyFor(method, url) };

    // The import endpoints only accept multipart uploads (anything else is a 415 before auth runs),
    // so they get a file to make sure it's the auth check that turns the request away.
    private static HttpContent? BodyFor(string method, string url)
    {
        if (method is "GET" or "DELETE") return null;
        if (!url.Contains("import")) return new StringContent("{}", Encoding.UTF8, "application/json");

        return new MultipartFormDataContent { { new ByteArrayContent("not really a workbook"u8.ToArray()), "file", "users.xlsx" } };
    }

    private static HttpClient WithCookie(HttpClient client, string cookie)
    {
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }

    private async Task<string> LoginCookieValueAsync(string path, string username, string password)
    {
        var response = await Factory.CreateClient().PostAsJsonAsync(path, new { username, password });
        response.EnsureSuccessStatusCode();
        var nameValue = response.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        return nameValue[(nameValue.IndexOf('=') + 1)..];
    }

    // --- Admin-only endpoints ---

    [Theory]
    [MemberData(nameof(AdminOnlyEndpoints))]
    public async Task AdminEndpoint_WithoutSession_ReturnsUnauthorized(string method, string url)
    {
        var response = await Factory.CreateClient().SendAsync(Request(method, url));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AdminOnlyEndpoints))]
    public async Task AdminEndpoint_WithUserSession_ReturnsUnauthorized(string method, string url)
    {
        var userClient = await CreateAuthenticatedUserClientAsync();
        var response = await userClient.SendAsync(Request(method, url));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AdminOnlyEndpoints))]
    public async Task AdminEndpoint_WithForgedAdminCookie_ReturnsUnauthorized(string method, string url)
    {
        var client = WithCookie(Factory.CreateClient(), $"{AdminCookieName}=CfDJ8forged-not-a-real-ticket");
        var response = await client.SendAsync(Request(method, url));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AdminOnlyEndpoints))]
    public async Task AdminEndpoint_WithUserTicketRenamedToAdminCookie_ReturnsUnauthorized(string method, string url)
    {
        // A voter copies their own valid ticket into the admin cookie name.
        var userTicket = await LoginCookieValueAsync("/api/auth/user/login", UserUsername, UserPassword);
        var client = WithCookie(Factory.CreateClient(), $"{AdminCookieName}={userTicket}");

        var response = await client.SendAsync(Request(method, url));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminCookie_WithOneCharacterTampered_ReturnsUnauthorized()
    {
        var ticket = await LoginCookieValueAsync("/api/auth/admin/login", AdminEmail, AdminPassword);
        var tampered = ticket[..^5] + (ticket[^5] == 'A' ? 'B' : 'A') + ticket[^4..];
        var client = WithCookie(Factory.CreateClient(), $"{AdminCookieName}={tampered}");

        var response = await client.GetAsync("/api/auth/admin/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- User-only endpoints ---

    [Theory]
    [MemberData(nameof(UserOnlyEndpoints))]
    public async Task UserEndpoint_WithAdminTicketRenamedToUserCookie_ReturnsUnauthorized(string method, string url)
    {
        var adminTicket = await LoginCookieValueAsync("/api/auth/admin/login", AdminEmail, AdminPassword);
        var client = WithCookie(Factory.CreateClient(), $"{UserCookieName}={adminTicket}");

        var response = await client.SendAsync(Request(method, url));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(UserOnlyEndpoints))]
    public async Task UserEndpoint_WithForgedUserCookie_ReturnsUnauthorized(string method, string url)
    {
        var client = WithCookie(Factory.CreateClient(), $"{UserCookieName}=CfDJ8forged-not-a-real-ticket");
        var response = await client.SendAsync(Request(method, url));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- Credentials don't cross between the two account tables ---

    [Fact]
    public async Task AdminLogin_WithVoterCredentials_ReturnsUnauthorized()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/auth/admin/login",
            new { username = UserUsername, password = UserPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserLogin_WithAdminCredentials_ReturnsUnauthorized()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/auth/user/login",
            new { username = AdminEmail, password = AdminPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- A voter's attack attempts leave no trace ---

    [Fact]
    public async Task VoterAttemptingAdminActions_ChangesNothing()
    {
        Guid victimId, optionId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Username = AttackerUsername,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(AttackerPassword),
                CreatedAt = DateTime.UtcNow
            });
            var option = new VotingOption { Id = Guid.NewGuid(), Name = "Option A", CreatedAt = DateTime.UtcNow };
            db.VotingOptions.Add(option);
            await db.SaveChangesAsync();
            victimId = (await db.Users.SingleAsync(u => u.Username == UserUsername)).Id;
            optionId = option.Id;
        }

        var attacker = await CreateAuthenticatedUserClientAsync(AttackerUsername, AttackerPassword);

        var attempts = new[]
        {
            await attacker.PutAsJsonAsync($"/api/users/{victimId}", new { username = UserUsername, password = NewPassword }),
            await attacker.PostAsJsonAsync("/api/administrators", new { username = "backdoor", email = "backdoor@example.com", password = NewPassword }),
            await attacker.PutAsJsonAsync($"/api/votes/{victimId}", new { votingOptionId = optionId }),
            await attacker.PutAsJsonAsync("/api/settings/voting", new { isVotingOpen = false }),
            await attacker.DeleteAsync("/api/voting-options"),
            await attacker.DeleteAsync("/api/users"),
        };

        Assert.All(attempts, r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
            Assert.Equal(2, await db.Users.CountAsync());
            Assert.Equal(1, await db.Administrators.CountAsync());
            Assert.Equal(1, await db.VotingOptions.CountAsync());
            Assert.Equal(0, await db.Votes.CountAsync());
            Assert.True((await db.VotingSettings.AsNoTracking().FirstOrDefaultAsync())?.IsVotingOpen ?? true);
        }

        var victimLogin = await Factory.CreateClient().PostAsJsonAsync("/api/auth/user/login",
            new { username = UserUsername, password = UserPassword });
        Assert.Equal(HttpStatusCode.OK, victimLogin.StatusCode);
    }
}
