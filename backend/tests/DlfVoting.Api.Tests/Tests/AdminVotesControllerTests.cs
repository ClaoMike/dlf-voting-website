using System.Net;
using System.Net.Http.Json;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DlfVoting.Api.Tests.Tests;

public class AdminVotesControllerTests : IntegrationTestBase
{
    private record VotingOptionResponseDto(Guid Id, string Name, DateTime CreatedAt);
    // ReSharper disable once ClassNeverInstantiated.Local
    // ReSharper disable once NotAccessedPositionalProperty.Local
    private record AdminVoteResponseDto(Guid UserId, string Email, Guid? VotingOptionId, string? VotingOptionName, DateTime? UpdatedAt);
    private record PagedVotesResponseDto(List<AdminVoteResponseDto> Items, int TotalCount, int Page, int PageSize);
    
    // ReSharper disable once ConvertToPrimaryConstructor
    public AdminVotesControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    private static async Task<Guid> CreateVotingOptionAsync(HttpClient adminClient, string name)
    {
        var response = await adminClient.PostAsJsonAsync("/api/voting-options", new { name });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<VotingOptionResponseDto>();
        return body!.Id;
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

    private async Task<Vote?> GetVoteForUserAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        return await db.Votes.FirstOrDefaultAsync(v => v.UserId == userId);
    }

    // --- Auth ---

    [Fact]
    public async Task GetAllPaged_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/votes");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAllPaged_WithUserAuth_ReturnsUnauthorized()
    {
        var userClient = await CreateAuthenticatedUserClientAsync();
        var response = await userClient.GetAsync("/api/votes");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAllPaged_WithAdminAuth_ReturnsOk()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var response = await adminClient.GetAsync("/api/votes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminSetVote_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.PutAsJsonAsync($"/api/votes/{Guid.NewGuid()}", new { votingOptionId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminSetVote_WithUserAuth_ReturnsUnauthorized()
    {
        var userClient = await CreateAuthenticatedUserClientAsync();
        var response = await userClient.PutAsJsonAsync($"/api/votes/{Guid.NewGuid()}", new { votingOptionId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminDeleteVote_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.DeleteAsync($"/api/votes/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminDeleteVote_WithUserAuth_ReturnsUnauthorized()
    {
        var userClient = await CreateAuthenticatedUserClientAsync();
        var response = await userClient.DeleteAsync($"/api/votes/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- GetAllPaged: correctness ---

    [Fact]
    public async Task GetAllPaged_ReturnsAllUsers_WithNullVoteInfoForNonVoters()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Sorted Option");

        var (_, voterClient) = await CreateAndLoginUserAsync("zzz-voter@example.com", "SomeValidPassword1!@#");
        await voterClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });

        await CreateAndLoginUserAsync("aaa-nonvoter@example.com", "SomeValidPassword2!@#");

        var response = await adminClient.GetAsync("/api/votes");
        var body = await response.Content.ReadFromJsonAsync<PagedVotesResponseDto>();

        // +1 accounts for the base seeded test user (UserEmail) from IntegrationTestBase, who never voted here.
        Assert.Equal(3, body!.TotalCount);

        var voterRow = body.Items.Single(v => v.Email == "zzz-voter@example.com");
        Assert.Equal(optionId, voterRow.VotingOptionId);
        Assert.Equal("Sorted Option", voterRow.VotingOptionName);

        var nonVoterRow = body.Items.Single(v => v.Email == "aaa-nonvoter@example.com");
        Assert.Null(nonVoterRow.VotingOptionId);
        Assert.Null(nonVoterRow.VotingOptionName);
        Assert.Null(nonVoterRow.UpdatedAt);
    }

    [Fact]
    public async Task GetAllPaged_ReturnsUpTo25ItemsSortedByEmail()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Bulk Option");

        for (var i = 0; i < 30; i++)
        {
            var (_, client) = await CreateAndLoginUserAsync($"voter{i:D2}@example.com", "SomeValidPassword1!@#");
            await client.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });
        }

        var response = await adminClient.GetAsync("/api/votes?page=1");
        var body = await response.Content.ReadFromJsonAsync<PagedVotesResponseDto>();

        // +1 accounts for the base seeded test user, who is a User but never voted.
        Assert.Equal(31, body!.TotalCount);
        Assert.Equal(25, body.PageSize);
        Assert.Equal(25, body.Items.Count);

        var emails = body.Items.Select(v => v.Email).ToList();
        var expected = emails.OrderBy(e => e, StringComparer.Ordinal).ToList();
        Assert.Equal(expected, emails);
    }

    [Fact]
    public async Task GetAllPaged_SecondPage_ReturnsRemainingItems()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Bulk Option 2");

        for (var i = 0; i < 30; i++)
        {
            var (_, client) = await CreateAndLoginUserAsync($"page2voter{i:D2}@example.com", "SomeValidPassword1!@#");
            await client.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });
        }

        var response = await adminClient.GetAsync("/api/votes?page=2");
        var body = await response.Content.ReadFromJsonAsync<PagedVotesResponseDto>();

        Assert.Equal(2, body!.Page);
        // 31 total (30 created + 1 base seeded user), 25 on page 1 → 6 remain.
        Assert.Equal(6, body.Items.Count);
    }

    // --- AdminSetVote ---

    [Fact]
    public async Task AdminSetVote_ForUserWithNoVote_CreatesVote()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Admin Assigned");
        var (userId, _) = await CreateAndLoginUserAsync("no-vote-yet@example.com", "SomeValidPassword1!@#");

        var response = await adminClient.PutAsJsonAsync($"/api/votes/{userId}", new { votingOptionId = optionId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var vote = await GetVoteForUserAsync(userId);
        Assert.Equal(optionId, vote!.VotingOptionId);
    }

    [Fact]
    public async Task AdminSetVote_ForUserWithExistingVote_UpdatesVote()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionA = await CreateVotingOptionAsync(adminClient, "Admin Update A");
        var optionB = await CreateVotingOptionAsync(adminClient, "Admin Update B");
        var (userId, userClient) = await CreateAndLoginUserAsync("existing-vote@example.com", "SomeValidPassword1!@#");
        await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionA });

        var response = await adminClient.PutAsJsonAsync($"/api/votes/{userId}", new { votingOptionId = optionB });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var vote = await GetVoteForUserAsync(userId);
        Assert.Equal(optionB, vote!.VotingOptionId);

        // Still exactly one row for this user, not two.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var count = await db.Votes.CountAsync(v => v.UserId == userId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task AdminSetVote_ForNonexistentUser_ReturnsNotFound()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Some Option");

        var response = await adminClient.PutAsJsonAsync($"/api/votes/{Guid.NewGuid()}", new { votingOptionId = optionId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdminSetVote_ForNonexistentOption_ReturnsNotFound()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var (userId, _) = await CreateAndLoginUserAsync("bad-option-target@example.com", "SomeValidPassword1!@#");

        var response = await adminClient.PutAsJsonAsync($"/api/votes/{userId}", new { votingOptionId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- AdminDeleteVote ---

    [Fact]
    public async Task AdminDeleteVote_ForUserWithVote_RemovesIt()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "To Be Removed By Admin");
        var (userId, userClient) = await CreateAndLoginUserAsync("delete-target@example.com", "SomeValidPassword1!@#");
        await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });

        var response = await adminClient.DeleteAsync($"/api/votes/{userId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await GetVoteForUserAsync(userId));
    }

    [Fact]
    public async Task AdminDeleteVote_ForUserWhoNeverVoted_ReturnsNotFound()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var (userId, _) = await CreateAndLoginUserAsync("never-voted@example.com", "SomeValidPassword1!@#");

        var response = await adminClient.DeleteAsync($"/api/votes/{userId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdminDeleteVote_AlreadyDeleted_ReturnsNotFound()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Double Delete By Admin");
        var (userId, userClient) = await CreateAndLoginUserAsync("admin-double-delete@example.com", "SomeValidPassword1!@#");
        await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });
        await adminClient.DeleteAsync($"/api/votes/{userId}");

        var secondResponse = await adminClient.DeleteAsync($"/api/votes/{userId}");

        Assert.Equal(HttpStatusCode.NotFound, secondResponse.StatusCode);
    }

    // --- Concurrency: admin acting alongside the user themselves ---

    [Fact]
    public async Task ConcurrentAdminAndUserEdit_SameVote_ResultsInExactlyOneVoteRow()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionA = await CreateVotingOptionAsync(adminClient, "Contested Admin A");
        var optionB = await CreateVotingOptionAsync(adminClient, "Contested Admin B");
        var (userId, userClient) = await CreateAndLoginUserAsync("contested-by-both@example.com", "SomeValidPassword1!@#");
        await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionA });

        // The user changes their own vote to B while the admin simultaneously sets it to A again.
        var userTask = userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionB });
        var adminTask = adminClient.PutAsJsonAsync($"/api/votes/{userId}", new { votingOptionId = optionA });
        var responses = await Task.WhenAll(userTask, adminTask);

        Assert.All(responses, r =>
            Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var count = await db.Votes.CountAsync(v => v.UserId == userId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ConcurrentAdminEditAndDelete_SameVote_NeverLeavesInconsistentState()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Edit Delete Race");
        var (userId, userClient) = await CreateAndLoginUserAsync("edit-delete-race@example.com", "SomeValidPassword1!@#");
        await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });

        var editTask = adminClient.PutAsJsonAsync($"/api/votes/{userId}", new { votingOptionId = optionId });
        var deleteTask = adminClient.DeleteAsync($"/api/votes/{userId}");
        var editResponse = await editTask;
        var deleteResponse = await deleteTask;

        Assert.True(
            editResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound or HttpStatusCode.Conflict);
        Assert.True(
            deleteResponse.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound);

        // Whatever order the race resolved in, there must be at most one row and no crash.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var count = await db.Votes.CountAsync(v => v.UserId == userId);
        Assert.True(count is 0 or 1);
    }

    [Fact]
    public async Task ConcurrentAdminDeleteVotes_DifferentUsers_AllSucceedIndependently()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Bulk Admin Delete Target");

        var (user1Id, client1) = await CreateAndLoginUserAsync("bulk-admin-del-1@example.com", "SomeValidPassword1!@#");
        var (user2Id, client2) = await CreateAndLoginUserAsync("bulk-admin-del-2@example.com", "SomeValidPassword2!@#");
        await client1.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });
        await client2.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });

        var task1 = adminClient.DeleteAsync($"/api/votes/{user1Id}");
        var task2 = adminClient.DeleteAsync($"/api/votes/{user2Id}");
        var responses = await Task.WhenAll(task1, task2);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
        Assert.Null(await GetVoteForUserAsync(user1Id));
        Assert.Null(await GetVoteForUserAsync(user2Id));
    }
    
    [Fact]
    public async Task ConcurrentTwoAdminsEditingSameVote_ResultsInExactlyOneVoteRow()
    {
        var adminClient1 = await CreateAuthenticatedClientAsync();
        var optionA = await CreateVotingOptionAsync(adminClient1, "Two Admins A");
        var optionB = await CreateVotingOptionAsync(adminClient1, "Two Admins B");
        var (userId, userClient) = await CreateAndLoginUserAsync("two-admins-target@example.com", "SomeValidPassword1!@#");
        await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionA });

        const string secondAdminEmail = "second-admin-vote-test@example.com";
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

        var loginClient = Factory.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/admin/login", new
        {
            email = secondAdminEmail,
            password = secondAdminPassword
        });
        var cookie = loginResponse.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        var adminClient2 = Factory.CreateClient();
        adminClient2.DefaultRequestHeaders.Add("Cookie", cookie);

        // Two different admins simultaneously set the same user's vote to two different options.
        var task1 = adminClient1.PutAsJsonAsync($"/api/votes/{userId}", new { votingOptionId = optionA });
        var task2 = adminClient2.PutAsJsonAsync($"/api/votes/{userId}", new { votingOptionId = optionB });
        var responses = await Task.WhenAll(task1, task2);

        Assert.All(responses, r =>
            Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var count = await verifyDb.Votes.CountAsync(v => v.UserId == userId);
        Assert.Equal(1, count);

        var finalVote = await verifyDb.Votes.FirstAsync(v => v.UserId == userId);
        Assert.True(finalVote.VotingOptionId == optionA || finalVote.VotingOptionId == optionB);
    }
    
    [Fact]
    public async Task GetAllPaged_OnlyVotedTrue_ReturnsOnlyUsersWhoVoted()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Only Voted Filter Option");

        var (_, voterClient) = await CreateAndLoginUserAsync("filter-voter@example.com", "SomeValidPassword1!@#");
        await voterClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });

        await CreateAndLoginUserAsync("filter-nonvoter@example.com", "SomeValidPassword2!@#");

        var response = await adminClient.GetAsync("/api/votes?onlyVoted=true");
        var body = await response.Content.ReadFromJsonAsync<PagedVotesResponseDto>();

        Assert.Equal(1, body!.TotalCount);
        Assert.Single(body.Items, v => v.Email == "filter-voter@example.com");
        Assert.DoesNotContain(body.Items, v => v.Email == "filter-nonvoter@example.com");
    }

    [Fact]
    public async Task GetAllPaged_OnlyVotedFalse_IsSameAsDefault()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        await CreateVotingOptionAsync(adminClient, "Explicit False Option");
        await CreateAndLoginUserAsync("explicit-false-nonvoter@example.com", "SomeValidPassword1!@#");

        var defaultResponse = await adminClient.GetAsync("/api/votes");
        var explicitResponse = await adminClient.GetAsync("/api/votes?onlyVoted=false");

        var defaultBody = await defaultResponse.Content.ReadFromJsonAsync<PagedVotesResponseDto>();
        var explicitBody = await explicitResponse.Content.ReadFromJsonAsync<PagedVotesResponseDto>();

        Assert.Equal(defaultBody!.TotalCount, explicitBody!.TotalCount);
    }

}