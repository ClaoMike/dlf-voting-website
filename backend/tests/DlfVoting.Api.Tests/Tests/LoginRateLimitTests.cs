using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

// ReSharper disable ClassNeverInstantiated.Local
// ReSharper disable NotAccessedPositionalProperty.Local

namespace DlfVoting.Api.Tests.Tests;

/// <summary>Sign-in attempts are limited per account (3 per window here; 10 per 5 minutes by default).</summary>
public class LoginRateLimitTests : IntegrationTestBase
{
    private record MessageDto(string Message);

    private const int Limit = 3;

    // ReSharper disable once ConvertToPrimaryConstructor
    public LoginRateLimitTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    // A fresh app per test, so every test starts with no attempts counted.
    private HttpClient CreateLimitedClient() =>
        Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("LoginRateLimit:PermitLimit", Limit.ToString());
            builder.UseSetting("LoginRateLimit:WindowSeconds", "300");
        }).CreateClient();

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string path, string username, string password) =>
        client.PostAsJsonAsync(path, new { username, password });

    [Theory]
    [InlineData("/api/auth/user/login", UserUsername)]
    [InlineData("/api/auth/admin/login", AdminEmail)]
    public async Task AfterTooManyWrongPasswords_TheAccountIsTemporarilyBlocked_EvenWithTheRightPassword(string path, string username)
    {
        var client = CreateLimitedClient();
        for (var i = 0; i < Limit; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, path, username, "Wrong-password-1234567!")).StatusCode);
        }

        var correctPassword = path.Contains("admin") ? AdminPassword : UserPassword;
        var blocked = await LoginAsync(client, path, username, correctPassword);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Equal("300", blocked.Headers.GetValues("Retry-After").Single());
        Assert.Equal(LoginAttemptLimiter.TooManyAttemptsMessage, (await blocked.Content.ReadFromJsonAsync<MessageDto>())!.Message);
        Assert.False(blocked.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task TheLimit_CountsDifferentCapitalisationAndSpacesAsTheSameAccount()
    {
        var client = CreateLimitedClient();
        foreach (var variant in new[] { "test-user", "TEST-USER", " Test-User " })
        {
            await LoginAsync(client, "/api/auth/user/login", variant, "Wrong-password-1234567!");
        }

        var response = await LoginAsync(client, "/api/auth/user/login", UserUsername, UserPassword);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task ABlockedAccount_DoesNotBlockOtherAccounts()
    {
        var client = CreateLimitedClient();
        for (var i = 0; i <= Limit; i++)
        {
            await LoginAsync(client, "/api/auth/user/login", "someone-else", "Wrong-password-1234567!");
        }

        var response = await LoginAsync(client, "/api/auth/user/login", UserUsername, UserPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task VoterAndAdministratorSignIns_AreCountedSeparately()
    {
        var client = CreateLimitedClient();
        for (var i = 0; i <= Limit; i++)
        {
            await LoginAsync(client, "/api/auth/admin/login", UserUsername, "Wrong-password-1234567!");
        }

        var response = await LoginAsync(client, "/api/auth/user/login", UserUsername, UserPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SuccessfulSignIns_AlsoCount()
    {
        var client = CreateLimitedClient();
        for (var i = 0; i < Limit; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, "/api/auth/user/login", UserUsername, UserPassword)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await LoginAsync(client, "/api/auth/user/login", UserUsername, UserPassword)).StatusCode);
    }
}
