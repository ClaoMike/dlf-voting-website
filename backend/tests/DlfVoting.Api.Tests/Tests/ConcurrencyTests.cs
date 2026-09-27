using System.Net;
using System.Net.Http.Json;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable ClassNeverInstantiated.Local
// ReSharper disable NotAccessedPositionalProperty.Local

namespace DlfVoting.Api.Tests.Tests;

/// <summary>Many voters at once, and admin actions racing against votes.</summary>
public class ConcurrencyTests : IntegrationTestBase
{
    private record VoteStatsDto(int TotalUsers, int VotedUsers, List<OptionVoteCountDto> OptionCounts);
    private record OptionVoteCountDto(Guid VotingOptionId, string VotingOptionName, int Count);

    private const string VoterPassword = "VoterPassword1234!@#$";

    // ReSharper disable once ConvertToPrimaryConstructor
    public ConcurrencyTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    /// <summary>Seeds <paramref name="count"/> voters (sharing one hash, BCrypt is slow) and logs them all in.</summary>
    private async Task<List<HttpClient>> CreateVotersAsync(int count)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(VoterPassword);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
            db.Users.AddRange(Enumerable.Range(1, count).Select(i => new User
            {
                Id = Guid.NewGuid(),
                Username = $"voter{i:D4}",
                PasswordHash = hash,
                CreatedAt = DateTime.UtcNow
            }));
            await db.SaveChangesAsync();
        }

        var clients = await Task.WhenAll(Enumerable.Range(1, count)
            .Select(i => CreateAuthenticatedUserClientAsync($"voter{i:D4}", VoterPassword)));
        return clients.ToList();
    }

    private async Task<List<Guid>> SeedVotingOptionsAsync(params string[] names)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var options = names.Select(n => new VotingOption { Id = Guid.NewGuid(), Name = n, CreatedAt = DateTime.UtcNow }).ToList();
        db.VotingOptions.AddRange(options);
        await db.SaveChangesAsync();
        return options.Select(o => o.Id).ToList();
    }

    private async Task<int> CountVotesAsync(Guid? optionId = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        return await db.Votes.CountAsync(v => optionId == null || v.VotingOptionId == optionId);
    }

    private async Task<Guid> SeedAdminAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var admin = new Administrator
        {
            Id = Guid.NewGuid(),
            Username = email,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(VoterPassword),
            CreatedAt = DateTime.UtcNow
        };
        db.Administrators.Add(admin);
        await db.SaveChangesAsync();
        return admin.Id;
    }

    private async Task<HttpClient> LoginAdminAsync(string email)
    {
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/auth/admin/login", new { username = email, password = VoterPassword });
        response.EnsureSuccessStatusCode();
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", response.Headers.GetValues("Set-Cookie").First().Split(';')[0]);
        return client;
    }

    [Fact]
    public async Task TwoHundredVotersAtOnce_AllVotesAreRecordedExactlyOnce()
    {
        var voters = await CreateVotersAsync(200);
        var options = await SeedVotingOptionsAsync("A", "B", "C");

        var responses = await Task.WhenAll(voters.Select((client, i) =>
            client.PostAsJsonAsync("/api/votes", new { votingOptionId = options[i % options.Count] })));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var admin = await CreateAuthenticatedClientAsync();
        var stats = await admin.GetFromJsonAsync<VoteStatsDto>("/api/votes/stats");
        Assert.Equal(200, stats!.VotedUsers);
        Assert.Equal(200, stats.OptionCounts.Sum(o => o.Count));
        Assert.Equal([67, 67, 66], stats.OptionCounts.Select(o => o.Count));
    }

    [Fact]
    public async Task SameVoterClickingManyTimesAtOnce_AlwaysSucceeds_AndKeepsOneVote()
    {
        var options = await SeedVotingOptionsAsync("A", "B", "C");
        var voter = await CreateAuthenticatedUserClientAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 30).Select(i =>
            voter.PostAsJsonAsync("/api/votes", new { votingOptionId = options[i % options.Count] })));

        // No "please try again" conflicts: a vote is a single upsert.
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(1, await CountVotesAsync());
    }

    [Fact]
    public async Task ClosingVotingWhileVotesAreInFlight_NoVoteLandsAfterTheCloseReturns()
    {
        var admin = await CreateAuthenticatedClientAsync();
        // Production always has the settings row (seeded at startup); the per-test reset removes it, so recreate it.
        (await admin.PutAsJsonAsync("/api/settings/voting", new { isVotingOpen = true })).EnsureSuccessStatusCode();
        var voters = await CreateVotersAsync(80);
        var optionId = (await SeedVotingOptionsAsync("A"))[0];

        var votes = voters.Select(c => c.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId })).ToList();
        var close = await admin.PutAsJsonAsync("/api/settings/voting", new { isVotingOpen = false });
        var votesWhenClosed = await CountVotesAsync();
        var responses = await Task.WhenAll(votes);

        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Forbidden }));
        Assert.Equal(votesWhenClosed, await CountVotesAsync());
        Assert.Equal(responses.Count(r => r.StatusCode == HttpStatusCode.OK), votesWhenClosed);
    }

    [Fact]
    public async Task DeletingAnOptionWhileVotesForItAreInFlight_NeverErrors_AndLeavesNoVotesForIt()
    {
        var voters = await CreateVotersAsync(60);
        var optionId = (await SeedVotingOptionsAsync("Doomed"))[0];
        var admin = await CreateAuthenticatedClientAsync();

        var votes = voters.Select(c => c.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId })).ToList();
        var delete = await admin.DeleteAsync($"/api/voting-options/{optionId}");
        var responses = await Task.WhenAll(votes);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }));
        Assert.Equal(0, await CountVotesAsync(optionId));
    }

    [Fact]
    public async Task TwoAdminsDeletingEachOtherAtOnce_OneOfThemAlwaysRemains()
    {
        for (var round = 0; round < 5; round++)
        {
            var aId = await SeedAdminAsync($"admin-a-{round}@example.com");
            var bId = await SeedAdminAsync($"admin-b-{round}@example.com");
            var a = await LoginAdminAsync($"admin-a-{round}@example.com");
            var b = await LoginAdminAsync($"admin-b-{round}@example.com");

            var responses = await Task.WhenAll(
                a.DeleteAsync($"/api/administrators/{bId}"),
                b.DeleteAsync($"/api/administrators/{aId}"));

            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
            Assert.Equal(1, await db.Administrators.CountAsync(x => x.Id == aId || x.Id == bId));
        }
    }

    [Fact]
    public async Task StatsReadDuringVoting_AreAlwaysInternallyConsistent()
    {
        var voters = await CreateVotersAsync(100);
        var options = await SeedVotingOptionsAsync("A", "B");
        var admin = await CreateAuthenticatedClientAsync();

        var votes = Task.WhenAll(voters.Select((c, i) => c.PostAsJsonAsync("/api/votes", new { votingOptionId = options[i % 2] })));
        var snapshots = new List<VoteStatsDto>();
        while (!votes.IsCompleted)
        {
            snapshots.Add((await admin.GetFromJsonAsync<VoteStatsDto>("/api/votes/stats"))!);
        }
        await votes;

        Assert.All(snapshots, s => Assert.Equal(s.VotedUsers, s.OptionCounts.Sum(o => o.Count)));
    }
}
