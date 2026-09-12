using System.Net;
using System.Net.Http.Json;

namespace DlfVoting.Api.Tests.Tests;

public class UserAuthControllerTests : IntegrationTestBase
{
    // ReSharper disable once ConvertToPrimaryConstructor
    public UserAuthControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsOkAndSetsCookie()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/user/login", new
        {
            email = UserEmail,
            password = UserPassword
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/user/login", new
        {
            email = UserEmail,
            password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/user/login", new
        {
            email = "nobody@example.com",
            password = "whatever"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutSession_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/auth/user/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_AfterLogin_ReturnsUserEmail()
    {
        var client = await CreateAuthenticatedUserClientAsync();

        var response = await client.GetAsync("/api/auth/user/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal(UserEmail, body?["email"]);
    }

    [Fact]
    public async Task Logout_ReturnsOkAndExpiresCookie()
    {
        var client = await CreateAuthenticatedUserClientAsync();

        var logoutResponse = await client.PostAsync("/api/auth/user/logout", null);
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        var setCookieHeader = logoutResponse.Headers.GetValues("Set-Cookie").First();
        Assert.Contains("expires=Thu, 01 Jan 1970", setCookieHeader, StringComparison.OrdinalIgnoreCase);
    }
}