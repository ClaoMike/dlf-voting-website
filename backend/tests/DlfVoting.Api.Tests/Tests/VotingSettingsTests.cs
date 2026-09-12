using System.Net;
using System.Net.Http.Json;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DlfVoting.Api.Tests;

public class VotingSettingsTests : IntegrationTestBase
{
    private record VotingStatusResponseDto(bool IsVotingOpen);
    private record VotingOptionResponseDto(Guid Id, string Name, DateTime CreatedAt);

    public VotingSettingsTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    private async Task SetVotingOpenAsync(bool isOpen)
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var response = await adminClient.PutAsJsonAsync("/api/settings/voting", new { isVotingOpen = isOpen });
        response.EnsureSuccessStatusCode();
    }

    private async Task<Guid> CreateVotingOptionAsync(HttpClient adminClient, string name)
    {
        var response = await adminClient.PostAsJsonAsync("/api/voting-options", new { name });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<VotingOptionResponseDto>();
        return body!.Id;
    }

    // --- GetStatus: auth ---

    [Fact]
    public async Task GetStatus_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/settings/voting");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetStatus_WithAdminAuth_ReturnsOk()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var response = await adminClient.GetAsync("/api/settings/voting");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStatus_WithUserAuth_ReturnsOk()
    {
        var userClient = await CreateAuthenticatedUserClientAsync();
        var response = await userClient.GetAsync("/api/settings/voting");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- GetStatus: correctness ---

    [Fact]
    public async Task GetStatus_DefaultsToOpen()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var response = await adminClient.GetAsync("/api/settings/voting");
        var body = await response.Content.ReadFromJsonAsync<VotingStatusResponseDto>();
        Assert.True(body!.IsVotingOpen);
    }

    // --- UpdateStatus: auth ---

    [Fact]
    public async Task UpdateStatus_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.PutAsJsonAsync("/api/settings/voting", new { isVotingOpen = false });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_WithUserAuth_ReturnsUnauthorized()
    {
        var userClient = await CreateAuthenticatedUserClientAsync();
        var response = await userClient.PutAsJsonAsync("/api/settings/voting", new { isVotingOpen = false });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- UpdateStatus: correctness ---

    [Fact]
    public async Task UpdateStatus_ClosingVoting_IsReflectedInSubsequentGetStatus()
    {
        var adminClient = await CreateAuthenticatedClientAsync();

        var updateResponse = await adminClient.PutAsJsonAsync("/api/settings/voting", new { isVotingOpen = false });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var getResponse = await adminClient.GetAsync("/api/settings/voting");
        var body = await getResponse.Content.ReadFromJsonAsync<VotingStatusResponseDto>();
        Assert.False(body!.IsVotingOpen);
    }

    [Fact]
    public async Task UpdateStatus_ReopeningVoting_IsReflectedInSubsequentGetStatus()
    {
        await SetVotingOpenAsync(false);
        await SetVotingOpenAsync(true);

        var adminClient = await CreateAuthenticatedClientAsync();
        var response = await adminClient.GetAsync("/api/settings/voting");
        var body = await response.Content.ReadFromJsonAsync<VotingStatusResponseDto>();

        Assert.True(body!.IsVotingOpen);
    }

    // --- Gating: VotesController ---

    [Fact]
    public async Task GetMyVote_WhenVotingClosed_ReturnsForbidden()
    {
        await SetVotingOpenAsync(false);
        var userClient = await CreateAuthenticatedUserClientAsync();

        var response = await userClient.GetAsync("/api/votes/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CastVote_WhenVotingClosed_ReturnsForbidden()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Gated Option");
        await SetVotingOpenAsync(false);

        var userClient = await CreateAuthenticatedUserClientAsync();
        var response = await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CastVote_WhenVotingReopened_SucceedsAgain()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Reopen Option");
        await SetVotingOpenAsync(false);
        await SetVotingOpenAsync(true);

        var userClient = await CreateAuthenticatedUserClientAsync();
        var response = await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMyVote_WhenVotingClosed_StillWorksForAdminSession()
    {
        // AdminSetVote/AdminDeleteVote/GetAllPaged are separate admin-only actions and
        // aren't decorated with [RequireVotingOpen] at all, so they should be entirely
        // unaffected by the toggle regardless of session type.
        await SetVotingOpenAsync(false);
        var adminClient = await CreateAuthenticatedClientAsync();

        var response = await adminClient.GetAsync("/api/votes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminSetVote_WhenVotingClosed_StillSucceeds()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Admin Override Option");
        var (userId, _) = await CreateAndLoginUserAsync("admin-override-target@example.com", "SomeValidPassword1!@#");
        await SetVotingOpenAsync(false);

        var response = await adminClient.PutAsJsonAsync($"/api/votes/{userId}", new { votingOptionId = optionId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<(Guid userId, HttpClient client)> CreateAndLoginUserAsync(string email, string password)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var client = await CreateAuthenticatedUserClientAsync(email, password);
        return (user.Id, client);
    }

    // --- Gating: VotingOptionsController ---

    [Fact]
    public async Task VotingOptions_GetAll_WhenVotingClosed_UserSessionGetsForbidden()
    {
        await SetVotingOpenAsync(false);
        var userClient = await CreateAuthenticatedUserClientAsync();

        var response = await userClient.GetAsync("/api/voting-options");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task VotingOptions_GetAll_WhenVotingClosed_AdminSessionStillSucceeds()
    {
        await SetVotingOpenAsync(false);
        var adminClient = await CreateAuthenticatedClientAsync();

        var response = await adminClient.GetAsync("/api/voting-options");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task VotingOptions_Create_WhenVotingClosed_StillSucceedsForAdmin()
    {
        // Admin management endpoints have no [RequireVotingOpen] at all, so creation,
        // editing, and deletion of options must remain fully available while closed.
        await SetVotingOpenAsync(false);
        var adminClient = await CreateAuthenticatedClientAsync();

        var response = await adminClient.PostAsJsonAsync("/api/voting-options", new { name = "Still Manageable" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- Gating does not affect auth endpoints ---

    [Fact]
    public async Task UserLogin_WhenVotingClosed_StillSucceeds()
    {
        await SetVotingOpenAsync(false);

        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/user/login", new
        {
            email = UserEmail,
            password = UserPassword
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UserMe_WhenVotingClosed_StillSucceeds()
    {
        await SetVotingOpenAsync(false);
        var userClient = await CreateAuthenticatedUserClientAsync();

        var response = await userClient.GetAsync("/api/auth/user/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- Concurrency ---

    [Fact]
    public async Task ConcurrentToggle_TwoAdmins_ResultsInOneConsistentFinalState()
    {
        const string secondAdminEmail = "settings-second-admin@example.com";
        const string secondAdminPassword = "AnotherStrongPassword1!@#";

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
            db.Administrators.Add(new Administrator
            {
                Id = Guid.NewGuid(),
                Email = secondAdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(secondAdminPassword),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var adminClient1 = await CreateAuthenticatedClientAsync();

        var loginClient = Factory.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/admin/login", new
        {
            email = secondAdminEmail,
            password = secondAdminPassword
        });
        var cookie = loginResponse.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        var adminClient2 = Factory.CreateClient();
        adminClient2.DefaultRequestHeaders.Add("Cookie", cookie);

        var task1 = adminClient1.PutAsJsonAsync("/api/settings/voting", new { isVotingOpen = false });
        var task2 = adminClient2.PutAsJsonAsync("/api/settings/voting", new { isVotingOpen = true });
        var responses = await Task.WhenAll(task1, task2);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        // Whichever wrote last "wins" — the important guarantee is a single, well-defined
        // final state (true or false), never a crash and never two conflicting rows.
        using var scope2 = Factory.Services.CreateScope();
        var verifyDb = scope2.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var rowCount = await verifyDb.VotingSettings.CountAsync();
        Assert.Equal(1, rowCount);
    }
}