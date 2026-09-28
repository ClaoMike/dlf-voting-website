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
    private record AdminVoteResponseDto(Guid UserId, string Username, string? FirstName, string? LastName, bool HasVoted);
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

    private async Task<(Guid userId, HttpClient client)> CreateAndLoginUserAsync(string username, string password)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var client = await CreateAuthenticatedUserClientAsync(username, password);
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
    public async Task AdminResetVote_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.DeleteAsync($"/api/votes/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminResetVote_WithUserAuth_ReturnsUnauthorized()
    {
        var userClient = await CreateAuthenticatedUserClientAsync();
        var response = await userClient.DeleteAsync($"/api/votes/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- GetAllPaged: correctness ---

    [Fact]
    public async Task GetAllPaged_ReturnsAllUsers_WithWhetherTheyVoted()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Sorted Option");

        var (_, voterClient) = await CreateAndLoginUserAsync("zzz-voter", "SomeValidPassword1!@#");
        await voterClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });

        await CreateAndLoginUserAsync("aaa-nonvoter", "SomeValidPassword2!@#");

        var response = await adminClient.GetAsync("/api/votes");
        var body = await response.Content.ReadFromJsonAsync<PagedVotesResponseDto>();

        // +1 accounts for the base seeded test user (UserUsername) from IntegrationTestBase, who never voted here.
        Assert.Equal(3, body!.TotalCount);

        Assert.True(body.Items.Single(v => v.Username == "zzz-voter").HasVoted);
        Assert.False(body.Items.Single(v => v.Username == "aaa-nonvoter").HasVoted);
    }

    [Fact]
    public async Task GetAllPaged_ReturnsUpTo25ItemsSortedByUsername()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Bulk Option");

        for (var i = 0; i < 30; i++)
        {
            var (_, client) = await CreateAndLoginUserAsync($"voter{i:D2}", "SomeValidPassword1!@#");
            await client.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });
        }

        var response = await adminClient.GetAsync("/api/votes?page=1");
        var body = await response.Content.ReadFromJsonAsync<PagedVotesResponseDto>();

        // +1 accounts for the base seeded test user, who is a User but never voted.
        Assert.Equal(31, body!.TotalCount);
        Assert.Equal(25, body.PageSize);
        Assert.Equal(25, body.Items.Count);

        var usernames = body.Items.Select(v => v.Username).ToList();
        var expected = usernames.OrderBy(u => u, StringComparer.Ordinal).ToList();
        Assert.Equal(expected, usernames);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetAllPaged_IsSortedByName_WithDanishLettersLast(bool onlyVoted)
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Name Sort Option");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
            (string Username, string First, string Last)[] people =
            [
                ("sort-aase", "Åse", "Berg"),
                ("sort-zenia", "Zenia", "Dahl"),
                ("sort-anders", "anders", "Holm"),
                ("sort-oersted", "Ørsted", "Lund"),
            ];
            foreach (var (username, first, last) in people)
            {
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Username = username,
                    FirstName = first,
                    LastName = last,
                    PasswordHash = "not-used",
                    CreatedAt = DateTime.UtcNow
                };
                db.Users.Add(user);
                db.Votes.Add(new Vote { Id = Guid.NewGuid(), UserId = user.Id, VotingOptionId = optionId, UpdatedAt = DateTime.UtcNow });
            }
            await db.SaveChangesAsync();
        }

        var response = await adminClient.GetAsync($"/api/votes?onlyVoted={onlyVoted}");
        var body = await response.Content.ReadFromJsonAsync<PagedVotesResponseDto>();

        var names = body!.Items.Where(v => v.FirstName is not null).Select(v => $"{v.FirstName} {v.LastName}").ToList();
        Assert.Equal(["anders Holm", "Zenia Dahl", "Ørsted Lund", "Åse Berg"], names);

        if (!onlyVoted)
        {
            // The seeded test user has no name and never voted: listed after everyone with a name.
            Assert.Equal(UserUsername, body.Items.Last().Username);
        }
    }

    [Fact]
    public async Task GetAllPaged_SecondPage_ReturnsRemainingItems()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Bulk Option 2");

        for (var i = 0; i < 30; i++)
        {
            var (_, client) = await CreateAndLoginUserAsync($"page2voter{i:D2}", "SomeValidPassword1!@#");
            await client.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });
        }

        var response = await adminClient.GetAsync("/api/votes?page=2");
        var body = await response.Content.ReadFromJsonAsync<PagedVotesResponseDto>();

        Assert.Equal(2, body!.Page);
        // 31 total (30 created + 1 base seeded user), 25 on page 1 → 6 remain.
        Assert.Equal(6, body.Items.Count);
    }

    // --- AdminResetVote ---

    [Fact]
    public async Task AdminResetVote_ForUserWithVote_DeletesIt()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "To Be Reset By Admin");
        var (userId, userClient) = await CreateAndLoginUserAsync("delete-target", "SomeValidPassword1!@#");
        await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });

        var response = await adminClient.DeleteAsync($"/api/votes/{userId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await GetVoteForUserAsync(userId));
    }

    [Fact]
    public async Task AdminResetVote_ForUserWhoNeverVoted_ReturnsNotFound()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var (userId, _) = await CreateAndLoginUserAsync("never-voted", "SomeValidPassword1!@#");

        var response = await adminClient.DeleteAsync($"/api/votes/{userId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdminResetVote_AlreadyReset_ReturnsNotFound()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Double Reset By Admin");
        var (userId, userClient) = await CreateAndLoginUserAsync("admin-double-reset", "SomeValidPassword1!@#");
        await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });
        await adminClient.DeleteAsync($"/api/votes/{userId}");

        var secondResponse = await adminClient.DeleteAsync($"/api/votes/{userId}");

        Assert.Equal(HttpStatusCode.NotFound, secondResponse.StatusCode);
    }

    // --- Concurrency: admin acting alongside the user themselves ---

    [Fact]
    public async Task ConcurrentUserChangeAndAdminReset_SameVote_NeverLeavesInconsistentState()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionA = await CreateVotingOptionAsync(adminClient, "Contested Admin A");
        var optionB = await CreateVotingOptionAsync(adminClient, "Contested Admin B");
        var (userId, userClient) = await CreateAndLoginUserAsync("contested-by-both", "SomeValidPassword1!@#");
        await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionA });

        // The user changes their own vote to B while the admin simultaneously resets it.
        var userTask = userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionB });
        var adminTask = adminClient.DeleteAsync($"/api/votes/{userId}");
        var userResponse = await userTask;
        var adminResponse = await adminTask;

        Assert.Equal(HttpStatusCode.OK, userResponse.StatusCode);
        Assert.True(adminResponse.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound);

        // Either the reset came last (no vote) or the user's change did (their new vote, B). Never two rows.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var votes = await db.Votes.Where(v => v.UserId == userId).ToListAsync();
        Assert.True(votes.Count is 0 or 1);
        Assert.All(votes, v => Assert.Equal(optionB, v.VotingOptionId));
    }

    [Fact]
    public async Task ConcurrentAdminResetVotes_DifferentUsers_AllSucceedIndependently()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Bulk Admin Reset Target");

        var (user1Id, client1) = await CreateAndLoginUserAsync("bulk-admin-del-1", "SomeValidPassword1!@#");
        var (user2Id, client2) = await CreateAndLoginUserAsync("bulk-admin-del-2", "SomeValidPassword2!@#");
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
    public async Task ConcurrentTwoAdminsResettingSameVote_ExactlyOneSucceeds()
    {
        var adminClient1 = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient1, "Two Admins Option");
        var (userId, userClient) = await CreateAndLoginUserAsync("two-admins-target", "SomeValidPassword1!@#");
        await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });

        const string secondAdminEmail = "second-admin-vote-test@example.com";
        const string secondAdminPassword = "AnotherStrongPassword1!@#";

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
            db.Administrators.Add(new Administrator
            {
                Id = Guid.NewGuid(),
                Username = secondAdminEmail,
                Email = secondAdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(secondAdminPassword),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var loginClient = Factory.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/admin/login", new
        {
            username = secondAdminEmail,
            password = secondAdminPassword
        });
        var cookie = loginResponse.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        var adminClient2 = Factory.CreateClient();
        adminClient2.DefaultRequestHeaders.Add("Cookie", cookie);

        // Two different admins simultaneously reset the same user's vote: one deletes it, the other finds it gone.
        var task1 = adminClient1.DeleteAsync($"/api/votes/{userId}");
        var task2 = adminClient2.DeleteAsync($"/api/votes/{userId}");
        var responses = await Task.WhenAll(task1, task2);

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NotFound);
        Assert.Null(await GetVoteForUserAsync(userId));
    }
    
    [Fact]
    public async Task GetAllPaged_OnlyVotedTrue_ReturnsOnlyUsersWhoVoted()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Only Voted Filter Option");

        var (_, voterClient) = await CreateAndLoginUserAsync("filter-voter", "SomeValidPassword1!@#");
        await voterClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });

        await CreateAndLoginUserAsync("filter-nonvoter", "SomeValidPassword2!@#");

        var response = await adminClient.GetAsync("/api/votes?onlyVoted=true");
        var body = await response.Content.ReadFromJsonAsync<PagedVotesResponseDto>();

        Assert.Equal(1, body!.TotalCount);
        Assert.Single(body.Items, v => v.Username == "filter-voter" && v.HasVoted);
        Assert.DoesNotContain(body.Items, v => v.Username == "filter-nonvoter");
    }

    [Fact]
    public async Task GetAllPaged_OnlyVotedFalse_IsSameAsDefault()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        await CreateVotingOptionAsync(adminClient, "Explicit False Option");
        await CreateAndLoginUserAsync("nonvoter-explicit", "SomeValidPassword1!@#");

        var defaultResponse = await adminClient.GetAsync("/api/votes");
        var explicitResponse = await adminClient.GetAsync("/api/votes?onlyVoted=false");

        var defaultBody = await defaultResponse.Content.ReadFromJsonAsync<PagedVotesResponseDto>();
        var explicitBody = await explicitResponse.Content.ReadFromJsonAsync<PagedVotesResponseDto>();

        Assert.Equal(defaultBody!.TotalCount, explicitBody!.TotalCount);
    }

}