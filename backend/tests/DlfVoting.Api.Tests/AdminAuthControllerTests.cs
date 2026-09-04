using System.Net;
using System.Net.Http.Json;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DlfVoting.Api.Tests;

public class AdminAuthControllerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly DatabaseFixture _dbFixture = new();

    private const string TestEmail = "test-admin@example.com";
    private const string TestPassword = "correct-horse-battery";

    public AdminAuthControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _dbFixture.InitializeAsync();
        await _dbFixture.ResetAsync();
        await SeedTestAdminAsync();
    }

    public Task DisposeAsync() => _dbFixture.DisposeAsync();

    private async Task SeedTestAdminAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();

        db.Administrators.Add(new Administrator
        {
            Id = Guid.NewGuid(),
            Email = TestEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(TestPassword),
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsOkAndSetsCookie()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/admin/login", new
        {
            email = TestEmail,
            password = TestPassword
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/admin/login", new
        {
            email = TestEmail,
            password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/admin/login", new
        {
            email = "nobody@example.com",
            password = "whatever"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutSession_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/admin/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_AfterLogin_ReturnsAdminEmail()
    {
        var handler = new HttpClientHandler { UseCookies = true, CookieContainer = new System.Net.CookieContainer() };
        var client = _factory.CreateDefaultClient();
        client.DefaultRequestHeaders.Add("Cookie", await LoginAndGetCookieAsync());

        var response = await client.GetAsync("/api/auth/admin/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal(TestEmail, body?["email"]);
    }

    [Fact]
    public async Task Logout_ReturnsOkAndExpiresCookie()
    {
        var cookie = await LoginAndGetCookieAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", cookie);

        var logoutResponse = await client.PostAsync("/api/auth/admin/logout", null);
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        var setCookieHeader = logoutResponse.Headers.GetValues("Set-Cookie").First();
        // A logout should tell the browser to delete the cookie via past expiry
        Assert.Contains("expires=Thu, 01 Jan 1970", setCookieHeader, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> LoginAndGetCookieAsync()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/admin/login", new
        {
            email = TestEmail,
            password = TestPassword
        });

        var setCookie = response.Headers.GetValues("Set-Cookie").First();
        return setCookie.Split(';')[0]; // just the "Name=Value" part
    }
}