using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace DlfVoting.Api.Tests;

public class MultiSessionTests : IntegrationTestBase
{
    public MultiSessionTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task AdminSession_DoesNotAuthorizeUserEndpoints()
    {
        var adminClient = await CreateAuthenticatedClientAsync();

        var response = await adminClient.GetAsync("/api/auth/user/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserSession_DoesNotAuthorizeAdminEndpoints()
    {
        var userClient = await CreateAuthenticatedUserClientAsync();

        var response = await userClient.GetAsync("/api/auth/admin/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserSession_DoesNotAuthorizeAdminOnlyResources()
    {
        var userClient = await CreateAuthenticatedUserClientAsync();

        var response = await userClient.GetAsync("/api/voting-options");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SameClient_CanHoldBothAdminAndUserCookiesSimultaneously()
    {
        // Log in as admin and as user using two separate HttpClients (each simulating
        // a distinct cookie jar, as separate browser tabs/sessions would behave),
        // then verify both remain independently valid at the same time.
        var adminClient = await CreateAuthenticatedClientAsync();
        var userClient = await CreateAuthenticatedUserClientAsync();

        var adminMeResponse = await adminClient.GetAsync("/api/auth/admin/me");
        var userMeResponse = await userClient.GetAsync("/api/auth/user/me");

        Assert.Equal(HttpStatusCode.OK, adminMeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, userMeResponse.StatusCode);

        var adminBody = await adminMeResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var userBody = await userMeResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        Assert.Equal(AdminEmail, adminBody?["email"]);
        Assert.Equal(UserEmail, userBody?["email"]);
    }

    [Fact]
    public async Task AdminLogout_DoesNotAffectConcurrentUserSession()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var userClient = await CreateAuthenticatedUserClientAsync();

        await adminClient.PostAsync("/api/auth/admin/logout", null);

        // The user's session, established independently, must remain valid.
        var userMeResponse = await userClient.GetAsync("/api/auth/user/me");
        Assert.Equal(HttpStatusCode.OK, userMeResponse.StatusCode);
    }

    [Fact]
    public async Task UserLogout_DoesNotAffectConcurrentAdminSession()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var userClient = await CreateAuthenticatedUserClientAsync();

        await userClient.PostAsync("/api/auth/user/logout", null);

        var adminMeResponse = await adminClient.GetAsync("/api/auth/admin/me");
        Assert.Equal(HttpStatusCode.OK, adminMeResponse.StatusCode);
    }

    [Fact]
    public async Task MultipleAdmins_CanBeLoggedInSimultaneously()
    {
        const string secondAdminEmail = "second-admin@example.com";
        const string secondAdminPassword = "another-strong-password!";

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DlfVoting.Infrastructure.DlfVotingDbContext>();
            db.Administrators.Add(new DlfVoting.Domain.Administrator
            {
                Id = Guid.NewGuid(),
                Email = secondAdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(secondAdminPassword),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var firstAdminClient = await CreateAuthenticatedClientAsync();

        var secondLoginClient = Factory.CreateClient();
        var secondLoginResponse = await secondLoginClient.PostAsJsonAsync("/api/auth/admin/login", new
        {
            email = secondAdminEmail,
            password = secondAdminPassword
        });
        var secondCookie = secondLoginResponse.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        var secondAdminClient = Factory.CreateClient();
        secondAdminClient.DefaultRequestHeaders.Add("Cookie", secondCookie);

        var firstMeResponse = await firstAdminClient.GetAsync("/api/auth/admin/me");
        var secondMeResponse = await secondAdminClient.GetAsync("/api/auth/admin/me");

        Assert.Equal(HttpStatusCode.OK, firstMeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondMeResponse.StatusCode);

        var firstBody = await firstMeResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var secondBody = await secondMeResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        Assert.Equal(AdminEmail, firstBody?["email"]);
        Assert.Equal(secondAdminEmail, secondBody?["email"]);
    }

    [Fact]
    public async Task MultipleUsers_CanBeLoggedInSimultaneously()
    {
        const string secondUserEmail = "second-user@example.com";
        const string secondUserPassword = "another-strong-password!!";

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DlfVoting.Infrastructure.DlfVotingDbContext>();
            db.Users.Add(new DlfVoting.Domain.User
            {
                Id = Guid.NewGuid(),
                Email = secondUserEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(secondUserPassword),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var firstUserClient = await CreateAuthenticatedUserClientAsync();
        var secondUserClient = await CreateAuthenticatedUserClientAsync(secondUserEmail, secondUserPassword);

        var firstMeResponse = await firstUserClient.GetAsync("/api/auth/user/me");
        var secondMeResponse = await secondUserClient.GetAsync("/api/auth/user/me");

        Assert.Equal(HttpStatusCode.OK, firstMeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondMeResponse.StatusCode);

        var firstBody = await firstMeResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var secondBody = await secondMeResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        Assert.Equal(UserEmail, firstBody?["email"]);
        Assert.Equal(secondUserEmail, secondBody?["email"]);
    }

    [Fact]
    public async Task OneUsersLogout_DoesNotAffectAnotherUsersSession()
    {
        const string secondUserEmail = "logout-test-user@example.com";
        const string secondUserPassword = "yet-another-password!!";

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DlfVoting.Infrastructure.DlfVotingDbContext>();
            db.Users.Add(new DlfVoting.Domain.User
            {
                Id = Guid.NewGuid(),
                Email = secondUserEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(secondUserPassword),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var firstUserClient = await CreateAuthenticatedUserClientAsync();
        var secondUserClient = await CreateAuthenticatedUserClientAsync(secondUserEmail, secondUserPassword);

        await firstUserClient.PostAsync("/api/auth/user/logout", null);

        var secondMeResponse = await secondUserClient.GetAsync("/api/auth/user/me");
        Assert.Equal(HttpStatusCode.OK, secondMeResponse.StatusCode);
    }
}