using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DlfVoting.Api.Tests.Tests;

/// <summary>
/// Admins used to see every user's vote and could set it (PUT /api/votes/{userId}). Both were removed so the vote stays
/// secret: these pin that the endpoint is gone for every kind of session, that calling it changes nothing, and that
/// the admin overview only says whether someone voted.
/// </summary>
public class RemovedAdminVoteEndpointsTests : IntegrationTestBase
{
    // ReSharper disable once ConvertToPrimaryConstructor
    public RemovedAdminVoteEndpointsTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    private async Task<(Guid userId, Guid optionId)> SeedUserAndOptionAsync(string username, bool withVote)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var user = new User { Id = Guid.NewGuid(), Username = username, PasswordHash = "not-used", CreatedAt = DateTime.UtcNow };
        var option = new VotingOption { Id = Guid.NewGuid(), Name = $"{username} option", CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        db.VotingOptions.Add(option);
        if (withVote)
        {
            db.Votes.Add(new Vote { Id = Guid.NewGuid(), UserId = user.Id, VotingOptionId = option.Id, UpdatedAt = DateTime.UtcNow });
        }
        await db.SaveChangesAsync();
        return (user.Id, option.Id);
    }

    private async Task<Guid> AddOptionAsync(string name)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var option = new VotingOption { Id = Guid.NewGuid(), Name = name, CreatedAt = DateTime.UtcNow };
        db.VotingOptions.Add(option);
        await db.SaveChangesAsync();
        return option.Id;
    }

    private async Task<Vote?> GetVoteAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        return await db.Votes.AsNoTracking().FirstOrDefaultAsync(v => v.UserId == userId);
    }

    // --- PUT /api/votes/{userId} (admin sets a user's vote) no longer exists ---

    [Fact]
    public async Task AdminSetVote_ForUserWithNoVote_IsNotFound_AndCreatesNoVote()
    {
        var (userId, optionId) = await SeedUserAndOptionAsync("removed-put-no-vote", withVote: false);
        var admin = await CreateAuthenticatedClientAsync();

        var response = await admin.PutAsJsonAsync($"/api/votes/{userId}", new { votingOptionId = optionId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(await GetVoteAsync(userId));
    }

    [Fact]
    public async Task AdminSetVote_ForUserWithVote_IsNotFound_AndLeavesVoteUnchanged()
    {
        var (userId, originalOptionId) = await SeedUserAndOptionAsync("removed-put-has-vote", withVote: true);
        var otherOptionId = await AddOptionAsync("Admin wants this one");
        var before = await GetVoteAsync(userId);
        var admin = await CreateAuthenticatedClientAsync();

        var response = await admin.PutAsJsonAsync($"/api/votes/{userId}", new { votingOptionId = otherOptionId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var after = await GetVoteAsync(userId);
        Assert.Equal(originalOptionId, after!.VotingOptionId);
        Assert.Equal(before!.UpdatedAt, after.UpdatedAt);
    }

    [Fact]
    public async Task AdminSetVote_WhenVotingClosed_IsNotFound_AndCreatesNoVote()
    {
        // Setting a vote while voting was closed used to be the admin's override; nothing can write a vote now.
        var (userId, optionId) = await SeedUserAndOptionAsync("removed-put-closed", withVote: false);
        var admin = await CreateAuthenticatedClientAsync();
        (await admin.PutAsJsonAsync("/api/settings/voting", new { isVotingOpen = false })).EnsureSuccessStatusCode();

        var response = await admin.PutAsJsonAsync($"/api/votes/{userId}", new { votingOptionId = optionId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(await GetVoteAsync(userId));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("user")]
    [InlineData("admin")]
    public async Task AdminSetVote_IsNotFoundForEverySession(string session)
    {
        // A 404 rather than a 401 for anonymous callers too: the route itself is gone, not just protected.
        var (userId, optionId) = await SeedUserAndOptionAsync($"removed-put-{session}", withVote: false);
        var client = session switch
        {
            "admin" => await CreateAuthenticatedClientAsync(),
            "user" => await CreateAuthenticatedUserClientAsync(),
            _ => Factory.CreateClient(),
        };

        var response = await client.PutAsJsonAsync($"/api/votes/{userId}", new { votingOptionId = optionId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(await GetVoteAsync(userId));
    }

    // --- Admins can't read an individual vote ---

    [Fact]
    public async Task AdminGetSingleVote_IsNotFound()
    {
        var (userId, _) = await SeedUserAndOptionAsync("removed-get-single", withVote: true);
        var admin = await CreateAuthenticatedClientAsync();

        var response = await admin.GetAsync($"/api/votes/{userId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminOverview_OnlySaysWhetherUserVoted_NeverWhatFor(bool onlyVoted)
    {
        var (userId, optionId) = await SeedUserAndOptionAsync("secret-ballot-voter", withVote: true);
        var admin = await CreateAuthenticatedClientAsync();

        var response = await admin.GetAsync($"/api/votes?onlyVoted={onlyVoted}");
        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);

        var row = json.RootElement.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("userId").GetGuid() == userId);
        var fields = row.EnumerateObject().Select(p => p.Name).Order().ToList();
        Assert.Equal(["firstName", "hasVoted", "lastName", "userId", "username"], fields);
        Assert.True(row.GetProperty("hasVoted").GetBoolean());

        // Nothing anywhere in the page points at the chosen option.
        Assert.DoesNotContain(optionId.ToString(), raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-ballot-voter option", raw);
    }
}
