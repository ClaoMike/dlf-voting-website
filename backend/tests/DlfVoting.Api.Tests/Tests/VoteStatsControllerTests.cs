using System.Net;
using System.Net.Http.Json;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DlfVoting.Api.Tests.Tests;

public class VoteStatsControllerTests : IntegrationTestBase
{
    private record VotingOptionResponseDto(Guid Id, string Name, DateTime CreatedAt);
    // ReSharper disable once ClassNeverInstantiated.Local
    // ReSharper disable once NotAccessedPositionalProperty.Local
    private record OptionVoteCountDto(Guid VotingOptionId, string VotingOptionName, int Count);
    private record VoteStatsResponseDto(int TotalUsers, int VotedUsers, List<OptionVoteCountDto> OptionCounts);
    
    // ReSharper disable once ConvertToPrimaryConstructor
    public VoteStatsControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    private static async Task<Guid> CreateVotingOptionAsync(HttpClient adminClient, string name)
    {
        var response = await adminClient.PostAsJsonAsync("/api/voting-options", new { name });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<VotingOptionResponseDto>();
        return body!.Id;
    }

    private async Task<HttpClient> CreateAndLoginUserAsync(string email, string password)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return await CreateAuthenticatedUserClientAsync(email, password);
    }

    // --- Auth ---

    [Fact]
    public async Task GetStats_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/votes/stats");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_WithUserAuth_ReturnsUnauthorized()
    {
        var userClient = await CreateAuthenticatedUserClientAsync();
        var response = await userClient.GetAsync("/api/votes/stats");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_WithAdminAuth_ReturnsOk()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var response = await adminClient.GetAsync("/api/votes/stats");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- Correctness ---

    [Fact]
    public async Task GetStats_WithNoVotingOptionsOrVotes_ReturnsZerosAndEmptyList()
    {
        var adminClient = await CreateAuthenticatedClientAsync();

        var response = await adminClient.GetAsync("/api/votes/stats");
        var body = await response.Content.ReadFromJsonAsync<VoteStatsResponseDto>();

        // The base seeded test user (from IntegrationTestBase) counts as a total user.
        Assert.Equal(1, body!.TotalUsers);
        Assert.Equal(0, body.VotedUsers);
        Assert.Empty(body.OptionCounts);
    }

    [Fact]
    public async Task GetStats_TotalUsers_ReflectsAllUsersRegardlessOfVoting()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        await CreateAndLoginUserAsync("stats-user-1@example.com", "SomeValidPassword1!@#");
        await CreateAndLoginUserAsync("stats-user-2@example.com", "SomeValidPassword2!@#");

        var response = await adminClient.GetAsync("/api/votes/stats");
        var body = await response.Content.ReadFromJsonAsync<VoteStatsResponseDto>();

        // +1 for the base seeded user.
        Assert.Equal(3, body!.TotalUsers);
        Assert.Equal(0, body.VotedUsers);
    }

    [Fact]
    public async Task GetStats_VotedUsers_ReflectsOnlyUsersWhoVoted()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Stats Option");

        var voter1 = await CreateAndLoginUserAsync("stats-voter-1@example.com", "SomeValidPassword1!@#");
        var voter2 = await CreateAndLoginUserAsync("stats-voter-2@example.com", "SomeValidPassword2!@#");
        await CreateAndLoginUserAsync("stats-nonvoter@example.com", "SomeValidPassword3!@#");

        await voter1.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });
        await voter2.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });

        var response = await adminClient.GetAsync("/api/votes/stats");
        var body = await response.Content.ReadFromJsonAsync<VoteStatsResponseDto>();

        Assert.Equal(2, body!.VotedUsers);
    }

    [Fact]
    public async Task GetStats_OptionCounts_IncludesOptionsWithZeroVotes()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var votedOptionId = await CreateVotingOptionAsync(adminClient, "Voted Option");
        var unvotedOptionId = await CreateVotingOptionAsync(adminClient, "Unvoted Option");

        var voter = await CreateAndLoginUserAsync("zero-votes-check@example.com", "SomeValidPassword1!@#");
        await voter.PostAsJsonAsync("/api/votes", new { votingOptionId = votedOptionId });

        var response = await adminClient.GetAsync("/api/votes/stats");
        var body = await response.Content.ReadFromJsonAsync<VoteStatsResponseDto>();

        var votedEntry = body!.OptionCounts.Single(o => o.VotingOptionId == votedOptionId);
        var unvotedEntry = body.OptionCounts.Single(o => o.VotingOptionId == unvotedOptionId);

        Assert.Equal(1, votedEntry.Count);
        Assert.Equal(0, unvotedEntry.Count);
    }

    [Fact]
    public async Task GetStats_OptionCounts_AreSortedDescending()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var lowOption = await CreateVotingOptionAsync(adminClient, "Low Votes");
        var midOption = await CreateVotingOptionAsync(adminClient, "Mid Votes");
        var highOption = await CreateVotingOptionAsync(adminClient, "High Votes");

        var voters = new List<HttpClient>();
        for (var i = 0; i < 5; i++)
        {
            voters.Add(await CreateAndLoginUserAsync($"sort-voter-{i}@example.com", "SomeValidPassword1!@#"));
        }

        // 1 vote for lowOption, 2 for midOption, ... wait, keep it simple:
        await voters[0].PostAsJsonAsync("/api/votes", new { votingOptionId = highOption });
        await voters[1].PostAsJsonAsync("/api/votes", new { votingOptionId = highOption });
        await voters[2].PostAsJsonAsync("/api/votes", new { votingOptionId = highOption });
        await voters[3].PostAsJsonAsync("/api/votes", new { votingOptionId = midOption });
        await voters[4].PostAsJsonAsync("/api/votes", new { votingOptionId = lowOption });

        var response = await adminClient.GetAsync("/api/votes/stats");
        var body = await response.Content.ReadFromJsonAsync<VoteStatsResponseDto>();

        var counts = body!.OptionCounts.Select(o => o.Count).ToList();
        var expected = counts.OrderByDescending(c => c).ToList();
        Assert.Equal(expected, counts);

        Assert.Equal(highOption, body.OptionCounts[0].VotingOptionId);
        Assert.Equal(3, body.OptionCounts[0].Count);
    }

    [Fact]
    public async Task GetStats_ReflectsVoteChanges()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionA = await CreateVotingOptionAsync(adminClient, "Change Stats A");
        var optionB = await CreateVotingOptionAsync(adminClient, "Change Stats B");
        var voter = await CreateAndLoginUserAsync("stats-change-voter@example.com", "SomeValidPassword1!@#");

        await voter.PostAsJsonAsync("/api/votes", new { votingOptionId = optionA });

        var beforeResponse = await adminClient.GetAsync("/api/votes/stats");
        var before = await beforeResponse.Content.ReadFromJsonAsync<VoteStatsResponseDto>();
        Assert.Equal(1, before!.OptionCounts.Single(o => o.VotingOptionId == optionA).Count);
        Assert.Equal(0, before.OptionCounts.Single(o => o.VotingOptionId == optionB).Count);

        // User changes their vote.
        await voter.PostAsJsonAsync("/api/votes", new { votingOptionId = optionB });

        var afterResponse = await adminClient.GetAsync("/api/votes/stats");
        var after = await afterResponse.Content.ReadFromJsonAsync<VoteStatsResponseDto>();
        Assert.Equal(0, after!.OptionCounts.Single(o => o.VotingOptionId == optionA).Count);
        Assert.Equal(1, after.OptionCounts.Single(o => o.VotingOptionId == optionB).Count);
        Assert.Equal(1, after.VotedUsers); // still just one voter, only their choice changed
    }

    [Fact]
    public async Task GetStats_ReflectsVoteRemoval()
    {
        var adminClient = await CreateAuthenticatedClientAsync();
        var optionId = await CreateVotingOptionAsync(adminClient, "Removal Stats Option");
        var voter = await CreateAndLoginUserAsync("stats-removal-voter@example.com", "SomeValidPassword1!@#");
        await voter.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == "stats-removal-voter@example.com");
            await adminClient.DeleteAsync($"/api/votes/{user.Id}");
        }

        var response = await adminClient.GetAsync("/api/votes/stats");
        var body = await response.Content.ReadFromJsonAsync<VoteStatsResponseDto>();

        Assert.Equal(0, body!.VotedUsers);
        Assert.Equal(0, body.OptionCounts.Single(o => o.VotingOptionId == optionId).Count);
    }
}