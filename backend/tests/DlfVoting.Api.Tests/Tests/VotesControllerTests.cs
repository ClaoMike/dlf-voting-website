using System.Net;
using System.Net.Http.Json;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DlfVoting.Api.Tests.Tests;

public class VotesControllerTests : IntegrationTestBase
{
    private record VotingOptionResponseDto(Guid Id, string Name, DateTime CreatedAt);
    private record MyVoteResponseDto(bool HasVoted, Guid? VotingOptionId, string? VotingOptionName, DateTime? UpdatedAt);
    
    // ReSharper disable once ConvertToPrimaryConstructor
    public VotesControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    private async Task<Guid> CreateVotingOptionAsync(string name)
    {
        var adminClient = await CreateAuthenticatedClientAsync();
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

    private async Task<int> GetVoteCountForUserAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        return await db.Votes.CountAsync(v => v.UserId == userId);
    }

    private async Task<Vote?> GetVoteForUserAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        return await db.Votes.FirstOrDefaultAsync(v => v.UserId == userId);
    }

    // --- Auth ---

    [Fact]
    public async Task GetMyVote_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/votes/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CastVote_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/votes", new { votingOptionId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyVote_WithAdminSession_ReturnsUnauthorized()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var response = await adminClient.GetAsync("/api/votes/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CastVote_WithAdminSession_ReturnsUnauthorized()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var response = await adminClient.PostAsJsonAsync("/api/votes", new { votingOptionId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- GetMyVote ---

    [Fact]
    public async Task GetMyVote_BeforeVoting_ReturnsHasVotedFalse()
    {
        var userClient = await CreateAuthenticatedUserClientAsync();

        var response = await userClient.GetAsync("/api/votes/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MyVoteResponseDto>();
        Assert.False(body!.HasVoted);
        Assert.Null(body.VotingOptionId);
        Assert.Null(body.VotingOptionName);
    }

    [Fact]
    public async Task GetMyVote_AfterVoting_ReturnsCorrectOption()
    {
        var optionId = await CreateVotingOptionAsync("Candidate A");
        var userClient = await CreateAuthenticatedUserClientAsync();

        await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });

        var response = await userClient.GetAsync("/api/votes/me");
        var body = await response.Content.ReadFromJsonAsync<MyVoteResponseDto>();

        Assert.True(body!.HasVoted);
        Assert.Equal(optionId, body.VotingOptionId);
        Assert.Equal("Candidate A", body.VotingOptionName);
    }

    // --- CastVote: basic cases ---

    [Fact]
    public async Task CastVote_WithValidOption_Succeeds()
    {
        var optionId = await CreateVotingOptionAsync("Option One");
        var userClient = await CreateAuthenticatedUserClientAsync();

        var response = await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MyVoteResponseDto>();
        Assert.True(body!.HasVoted);
        Assert.Equal(optionId, body.VotingOptionId);
    }

    [Fact]
    public async Task CastVote_WithNonexistentOption_ReturnsNotFound()
    {
        var userClient = await CreateAuthenticatedUserClientAsync();

        var response = await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CastVote_FirstTime_CreatesExactlyOneVoteRow()
    {
        var optionId = await CreateVotingOptionAsync("Only Choice");

        var userClient = await CreateAuthenticatedUserClientAsync();
        await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var seededUser = await verifyDb.Users.FirstAsync(u => u.Email == UserEmail);
        var voteCount = await verifyDb.Votes.CountAsync(v => v.UserId == seededUser.Id);

        Assert.Equal(1, voteCount);
    }

    // --- CastVote: changing vote ---

    [Fact]
    public async Task CastVote_ChangingVote_UpdatesExistingRowInsteadOfCreatingNew()
    {
        var optionA = await CreateVotingOptionAsync("Option A");
        var optionB = await CreateVotingOptionAsync("Option B");
        var userClient = await CreateAuthenticatedUserClientAsync();

        await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionA });
        var secondResponse = await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionB });

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var seededUser = await db.Users.FirstAsync(u => u.Email == UserEmail);
        var votes = await db.Votes.Where(v => v.UserId == seededUser.Id).ToListAsync();

        Assert.Single(votes);
        Assert.Equal(optionB, votes[0].VotingOptionId);
    }

    [Fact]
    public async Task CastVote_ForSameOptionTwice_StillSucceeds()
    {
        var optionId = await CreateVotingOptionAsync("Repeat Choice");
        var userClient = await CreateAuthenticatedUserClientAsync();

        await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });
        var response = await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CastVote_ForDeletedOption_ReturnsNotFound()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var createResponse = await adminClient.PostAsJsonAsync("/api/voting-options", new { name = "Soon Deleted" });
        var created = await createResponse.Content.ReadFromJsonAsync<VotingOptionResponseDto>();
        await adminClient.DeleteAsync($"/api/voting-options/{created!.Id}");

        var userClient = await CreateAuthenticatedUserClientAsync();
        var response = await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = created.Id });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Different users vote independently ---

    [Fact]
    public async Task TwoDifferentUsers_CanVoteForTheSameOption()
    {
        var optionId = await CreateVotingOptionAsync("Popular Choice");
        var (user1Id, client1) = await CreateAndLoginUserAsync("voter1@example.com", "SomeValidPassword1!@#");
        var (user2Id, client2) = await CreateAndLoginUserAsync("voter2@example.com", "SomeValidPassword2!@#");

        var response1 = await client1.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });
        var response2 = await client2.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });

        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var totalVotesForOption = await db.Votes.CountAsync(v => v.VotingOptionId == optionId);
        Assert.Equal(2, totalVotesForOption);
    }

    [Fact]
    public async Task OneUsersVote_DoesNotAffectAnotherUsersVote()
    {
        var optionA = await CreateVotingOptionAsync("Choice A");
        var optionB = await CreateVotingOptionAsync("Choice B");
        var (user1Id, client1) = await CreateAndLoginUserAsync("independent1@example.com", "SomeValidPassword1!@#");
        var (user2Id, client2) = await CreateAndLoginUserAsync("independent2@example.com", "SomeValidPassword2!@#");

        await client1.PostAsJsonAsync("/api/votes", new { votingOptionId = optionA });
        await client2.PostAsJsonAsync("/api/votes", new { votingOptionId = optionB });

        var vote1 = await GetVoteForUserAsync(user1Id);
        var vote2 = await GetVoteForUserAsync(user2Id);

        Assert.Equal(optionA, vote1!.VotingOptionId);
        Assert.Equal(optionB, vote2!.VotingOptionId);
    }

    // --- Concurrency: same user, racing requests ---

    [Fact]
    public async Task ConcurrentCastVote_SameUserSameOption_ResultsInExactlyOneVoteRow()
    {
        var optionId = await CreateVotingOptionAsync("Race Target");
        var (userId, client) = await CreateAndLoginUserAsync("race-same-option@example.com", "SomeValidPassword1!@#");

        var task1 = client.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });
        var task2 = client.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });
        var responses = await Task.WhenAll(task1, task2);

        // Every response must be either a success or a clean conflict — never an unhandled error.
        Assert.All(responses, r =>
            Assert.True(r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Conflict));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);

        var voteCount = await GetVoteCountForUserAsync(userId);
        Assert.Equal(1, voteCount);

        var vote = await GetVoteForUserAsync(userId);
        Assert.Equal(optionId, vote!.VotingOptionId);
    }

    [Fact]
    public async Task ConcurrentCastVote_SameUserDifferentOptions_ResultsInExactlyOneVoteRowForEitherOption()
    {
        var optionA = await CreateVotingOptionAsync("Race Option A");
        var optionB = await CreateVotingOptionAsync("Race Option B");
        var (userId, client) = await CreateAndLoginUserAsync("race-diff-options@example.com", "SomeValidPassword1!@#");

        var task1 = client.PostAsJsonAsync("/api/votes", new { votingOptionId = optionA });
        var task2 = client.PostAsJsonAsync("/api/votes", new { votingOptionId = optionB });
        var responses = await Task.WhenAll(task1, task2);

        Assert.All(responses, r =>
            Assert.True(r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Conflict));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);

        var voteCount = await GetVoteCountForUserAsync(userId);
        Assert.Equal(1, voteCount);

        var vote = await GetVoteForUserAsync(userId);
        Assert.True(vote!.VotingOptionId == optionA || vote.VotingOptionId == optionB);
    }

    // --- Concurrency: multiple different users racing ---

    [Fact]
    public async Task ConcurrentVotes_MultipleUsersVotingForSameOption_AllSucceedIndependently()
    {
        var optionId = await CreateVotingOptionAsync("Everyone's Favorite");

        var (user1Id, client1) = await CreateAndLoginUserAsync("multi-same-1@example.com", "SomeValidPassword1!@#");
        var (user2Id, client2) = await CreateAndLoginUserAsync("multi-same-2@example.com", "SomeValidPassword2!@#");
        var (user3Id, client3) = await CreateAndLoginUserAsync("multi-same-3@example.com", "SomeValidPassword3!@#");

        var task1 = client1.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });
        var task2 = client2.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });
        var task3 = client3.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });
        var responses = await Task.WhenAll(task1, task2, task3);

        // Different users never contend for the same DB row (unique index is per-user),
        // so all three must succeed cleanly with no conflicts at all.
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var totalVotesForOption = await db.Votes.CountAsync(v => v.VotingOptionId == optionId);
        Assert.Equal(3, totalVotesForOption);

        Assert.Equal(1, await GetVoteCountForUserAsync(user1Id));
        Assert.Equal(1, await GetVoteCountForUserAsync(user2Id));
        Assert.Equal(1, await GetVoteCountForUserAsync(user3Id));
    }

    [Fact]
    public async Task ConcurrentVotes_MultipleUsersVotingForDifferentOptions_AllSucceedIndependently()
    {
        var optionA = await CreateVotingOptionAsync("Multi Option A");
        var optionB = await CreateVotingOptionAsync("Multi Option B");
        var optionC = await CreateVotingOptionAsync("Multi Option C");

        var (user1Id, client1) = await CreateAndLoginUserAsync("multi-diff-1@example.com", "SomeValidPassword1!@#");
        var (user2Id, client2) = await CreateAndLoginUserAsync("multi-diff-2@example.com", "SomeValidPassword2!@#");
        var (user3Id, client3) = await CreateAndLoginUserAsync("multi-diff-3@example.com", "SomeValidPassword3!@#");

        var task1 = client1.PostAsJsonAsync("/api/votes", new { votingOptionId = optionA });
        var task2 = client2.PostAsJsonAsync("/api/votes", new { votingOptionId = optionB });
        var task3 = client3.PostAsJsonAsync("/api/votes", new { votingOptionId = optionC });
        var responses = await Task.WhenAll(task1, task2, task3);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        var vote1 = await GetVoteForUserAsync(user1Id);
        var vote2 = await GetVoteForUserAsync(user2Id);
        var vote3 = await GetVoteForUserAsync(user3Id);

        Assert.Equal(optionA, vote1!.VotingOptionId);
        Assert.Equal(optionB, vote2!.VotingOptionId);
        Assert.Equal(optionC, vote3!.VotingOptionId);
    }

    [Fact]
    public async Task ConcurrentVotes_MixOfSameAndDifferentOptions_EachUserEndsUpWithExactlyOneVote()
    {
        // A broader stress case: five users, two of them voting for the same option
        // simultaneously with three others voting for distinct options at the same time.
        var optionA = await CreateVotingOptionAsync("Stress Option A");
        var optionB = await CreateVotingOptionAsync("Stress Option B");

        var (user1Id, client1) = await CreateAndLoginUserAsync("stress-1@example.com", "SomeValidPassword1!@#");
        var (user2Id, client2) = await CreateAndLoginUserAsync("stress-2@example.com", "SomeValidPassword2!@#");
        var (user3Id, client3) = await CreateAndLoginUserAsync("stress-3@example.com", "SomeValidPassword3!@#");
        var (user4Id, client4) = await CreateAndLoginUserAsync("stress-4@example.com", "SomeValidPassword4!@#");
        var (user5Id, client5) = await CreateAndLoginUserAsync("stress-5@example.com", "SomeValidPassword5!@#");

        var tasks = new[]
        {
            client1.PostAsJsonAsync("/api/votes", new { votingOptionId = optionA }),
            client2.PostAsJsonAsync("/api/votes", new { votingOptionId = optionA }),
            client3.PostAsJsonAsync("/api/votes", new { votingOptionId = optionB }),
            client4.PostAsJsonAsync("/api/votes", new { votingOptionId = optionB }),
            client5.PostAsJsonAsync("/api/votes", new { votingOptionId = optionA }),
        };
        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var votesForA = await db.Votes.CountAsync(v => v.VotingOptionId == optionA);
        var votesForB = await db.Votes.CountAsync(v => v.VotingOptionId == optionB);

        Assert.Equal(3, votesForA);
        Assert.Equal(2, votesForB);

        foreach (var userId in new[] { user1Id, user2Id, user3Id, user4Id, user5Id })
        {
            Assert.Equal(1, await GetVoteCountForUserAsync(userId));
        }
    }
}