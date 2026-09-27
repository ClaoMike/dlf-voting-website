using System.Net;
using System.Net.Http.Json;
using System.Text;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DlfVoting.Api.Tests.Tests;

/// <summary>Cookie flags, session lifetime, sessions of deleted accounts, CORS and CSRF.</summary>
public class SessionSecurityTests : IntegrationTestBase
{
    private const string FrontendOrigin = "http://localhost:5173";
    private const string EvilOrigin = "https://evil.example";
    private const string OtherAdminEmail = "other-admin@example.com";
    private const string OtherAdminPassword = "OtherAdminPassword1234!@#";
    private const string ValidPassword = "ValidPassword1234!@#$";

    // ReSharper disable once ConvertToPrimaryConstructor
    public SessionSecurityTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    /// <summary>System time shifted by <see cref="Offset"/>; the cookie handlers read the clock from DI.</summary>
    private sealed class AdjustableTimeProvider : TimeProvider
    {
        public TimeSpan Offset { get; set; }
        public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + Offset;
    }

    private WebApplicationFactory<Program> FactoryWithClock(AdjustableTimeProvider clock) =>
        Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> factory, string path, string username, string password)
    {
        var response = await factory.CreateClient().PostAsJsonAsync(path, new { username, password });
        response.EnsureSuccessStatusCode();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", response.Headers.GetValues("Set-Cookie").First().Split(';')[0]);
        return client;
    }

    private async Task<Guid> SeedAdminAsync(string email, string password)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var admin = new Administrator
        {
            Id = Guid.NewGuid(),
            Username = email,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTime.UtcNow
        };
        db.Administrators.Add(admin);
        await db.SaveChangesAsync();
        return admin.Id;
    }

    private async Task<Guid> SeedVotingOptionAsync(string name)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var option = new VotingOption { Id = Guid.NewGuid(), Name = name, CreatedAt = DateTime.UtcNow };
        db.VotingOptions.Add(option);
        await db.SaveChangesAsync();
        return option.Id;
    }

    private async Task<Guid> GetSeededUserIdAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        return (await db.Users.SingleAsync(u => u.Username == UserUsername)).Id;
    }

    // --- Cookie flags ---

    [Theory]
    [InlineData("/api/auth/admin/login", AdminEmail, AdminPassword)]
    [InlineData("/api/auth/user/login", UserUsername, UserPassword)]
    public async Task LoginCookie_IsHttpOnlyAndSameSiteLax(string path, string username, string password)
    {
        var response = await Factory.CreateClient().PostAsJsonAsync(path, new { username, password });

        var setCookie = response.Headers.GetValues("Set-Cookie").First();
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/api/auth/admin/login", AdminEmail, AdminPassword)]
    [InlineData("/api/auth/user/login", UserUsername, UserPassword)]
    public async Task LoginCookie_OverHttps_IsSecure(string path, string username, string password)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var response = await client.PostAsJsonAsync(path, new { username, password });

        var setCookie = response.Headers.GetValues("Set-Cookie").First();
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/api/auth/admin/login", AdminEmail, AdminPassword)]
    [InlineData("/api/auth/user/login", UserUsername, UserPassword)]
    public async Task LoginCookie_OutsideDevelopment_IsSecureEvenWhenTheRequestArrivesOverHttp(string path, string username, string password)
    {
        // On Azure, TLS ends in front of the app and the request reaches it as plain http.
        var response = await Factory.CreateClient().PostAsJsonAsync(path, new { username, password });

        var setCookie = response.Headers.GetValues("Set-Cookie").First();
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/api/auth/admin/login", AdminEmail, AdminPassword)]
    [InlineData("/api/auth/user/login", UserUsername, UserPassword)]
    public async Task LoginCookie_IsSessionCookieWithoutPersistentExpiry(string path, string username, string password)
    {
        var response = await Factory.CreateClient().PostAsJsonAsync(path, new { username, password });

        var setCookie = response.Headers.GetValues("Set-Cookie").First();
        Assert.DoesNotContain("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("max-age=", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    // --- Five-minute session lifetime ---

    [Theory]
    [InlineData("/api/auth/admin/login", "/api/auth/admin/me", AdminEmail, AdminPassword)]
    [InlineData("/api/auth/user/login", "/api/auth/user/me", UserUsername, UserPassword)]
    public async Task Session_IsValidAtFourMinutes_AndExpiredAfterFive(string loginPath, string mePath, string username, string password)
    {
        var clock = new AdjustableTimeProvider();
        var factory = FactoryWithClock(clock);
        var client = await LoginAsync(factory, loginPath, username, password);

        clock.Offset = TimeSpan.FromMinutes(4);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(mePath)).StatusCode);

        clock.Offset = TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(30);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(mePath)).StatusCode);
    }

    [Fact]
    public async Task Session_IsNotExtendedByActivity()
    {
        var clock = new AdjustableTimeProvider();
        var factory = FactoryWithClock(clock);
        var client = await LoginAsync(factory, "/api/auth/admin/login", AdminEmail, AdminPassword);

        clock.Offset = TimeSpan.FromMinutes(4);
        var midSession = await client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.OK, midSession.StatusCode);
        Assert.False(midSession.Headers.Contains("Set-Cookie"), "An authenticated request must not re-issue (slide) the cookie.");

        clock.Offset = TimeSpan.FromMinutes(6);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task ExpiredAdminSession_CannotPerformWrites()
    {
        var clock = new AdjustableTimeProvider();
        var factory = FactoryWithClock(clock);
        var client = await LoginAsync(factory, "/api/auth/admin/login", AdminEmail, AdminPassword);

        clock.Offset = TimeSpan.FromMinutes(10);
        var response = await client.DeleteAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        Assert.Equal(1, await db.Users.CountAsync());
    }

    // --- Sessions of deleted accounts ---

    [Fact]
    public async Task DeletedAdmin_ExistingSession_CannotCreateABackdoorAdministrator()
    {
        var otherAdminId = await SeedAdminAsync(OtherAdminEmail, OtherAdminPassword);
        var removedAdmin = await LoginAsync(Factory, "/api/auth/admin/login", OtherAdminEmail, OtherAdminPassword);
        var admin = await CreateAuthenticatedClientAsync();

        var delete = await admin.DeleteAsync($"/api/administrators/{otherAdminId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var response = await removedAdmin.PostAsJsonAsync("/api/administrators",
            new { username = "backdoor", email = "backdoor@example.com", password = ValidPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        Assert.False(await db.Administrators.AnyAsync(a => a.Username == "backdoor"));
    }

    [Fact]
    public async Task DeletedAdmin_ExistingSession_CannotReadUsers()
    {
        var otherAdminId = await SeedAdminAsync(OtherAdminEmail, OtherAdminPassword);
        var removedAdmin = await LoginAsync(Factory, "/api/auth/admin/login", OtherAdminEmail, OtherAdminPassword);
        var admin = await CreateAuthenticatedClientAsync();
        (await admin.DeleteAsync($"/api/administrators/{otherAdminId}")).EnsureSuccessStatusCode();

        var response = await removedAdmin.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeletedUser_ExistingSession_CannotVote()
    {
        var optionId = await SeedVotingOptionAsync("Option A");
        var userClient = await CreateAuthenticatedUserClientAsync();
        var admin = await CreateAuthenticatedClientAsync();
        (await admin.DeleteAsync($"/api/users/{await GetSeededUserIdAsync()}")).EnsureSuccessStatusCode();

        var response = await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        Assert.Equal(0, await db.Votes.CountAsync());
    }

    [Fact]
    public async Task DeletedUser_ExistingSession_CannotReadVotingOptions()
    {
        await SeedVotingOptionAsync("Option A");
        var userClient = await CreateAuthenticatedUserClientAsync();
        var admin = await CreateAuthenticatedClientAsync();
        (await admin.DeleteAsync($"/api/users/{await GetSeededUserIdAsync()}")).EnsureSuccessStatusCode();

        var response = await userClient.GetAsync("/api/voting-options");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- CORS ---

    [Fact]
    public async Task Cors_FrontendOrigin_IsAllowedWithCredentials()
    {
        var client = await CreateAuthenticatedClientAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/users");
        request.Headers.Add("Origin", FrontendOrigin);

        var response = await client.SendAsync(request);

        Assert.Equal(FrontendOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    [Fact]
    public async Task Cors_ForeignOrigin_GetsNoAllowOriginHeader()
    {
        var client = await CreateAuthenticatedClientAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/users");
        request.Headers.Add("Origin", EvilOrigin);

        var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Theory]
    [InlineData("DELETE", "/api/users")]
    [InlineData("PUT", "/api/settings/voting")]
    [InlineData("POST", "/api/votes")]
    public async Task Cors_PreflightFromForeignOrigin_IsNotApproved(string method, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, url);
        request.Headers.Add("Origin", EvilOrigin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await Factory.CreateClient().SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Methods"));
    }

    // --- CSRF: endpoints only accept JSON, so a cross-site HTML form can't drive them ---

    [Theory]
    [InlineData("application/x-www-form-urlencoded")]
    [InlineData("multipart/form-data")]
    [InlineData("text/plain")]
    public async Task CastVote_WithFormLikeContentType_IsRejectedAndRecordsNothing(string contentType)
    {
        var optionId = await SeedVotingOptionAsync("Option A");
        var userClient = await CreateAuthenticatedUserClientAsync();
        var content = new StringContent($"{{\"votingOptionId\":\"{optionId}\"}}", Encoding.UTF8);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

        var response = await userClient.PostAsync("/api/votes", content);

        // 415, or 400 when a multipart body can't even be parsed; either way the vote endpoint never runs.
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.UnsupportedMediaType, HttpStatusCode.BadRequest });
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        Assert.Equal(0, await db.Votes.CountAsync());
    }

    [Fact]
    public async Task AdminLogin_WithFormEncodedBody_IsRejected()
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = AdminEmail,
            ["password"] = AdminPassword
        });

        var response = await Factory.CreateClient().PostAsync("/api/auth/admin/login", form);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task CreateAdministrator_WithTextPlainBody_IsRejected()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var body = new StringContent(
            $"{{\"username\":\"backdoor\",\"email\":\"backdoor@example.com\",\"password\":\"{ValidPassword}\"}}",
            Encoding.UTF8, "text/plain");

        var response = await admin.PostAsync("/api/administrators", body);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }
}
