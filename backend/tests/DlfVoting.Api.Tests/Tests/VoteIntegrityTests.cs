using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable ClassNeverInstantiated.Local
// ReSharper disable NotAccessedPositionalProperty.Local

namespace DlfVoting.Api.Tests.Tests;

/// <summary>
/// One person, one vote, counted correctly: no voting on someone else's behalf, no duplicate accounts
/// for the same email, no orphaned votes, and no secrets in responses or exports.
/// </summary>
public class VoteIntegrityTests : IntegrationTestBase
{
    private record MyVoteResponseDto(bool HasVoted, Guid? VotingOptionId, string? VotingOptionName, DateTime? UpdatedAt);
    private record VoteStatsDto(int TotalUsers, int VotedUsers, List<OptionVoteCountDto> OptionCounts);
    private record OptionVoteCountDto(Guid VotingOptionId, string VotingOptionName, int Count);
    private record BulkImportSkippedEntryDto(string Email, string Reason);
    private record BulkImportedUserDto(string Email, string Username, string Password);
    private record BulkImportResponseDto(List<BulkImportedUserDto> Created, List<BulkImportSkippedEntryDto> Skipped, string? File);
    private record MessageDto(string Message);

    private const string OtherUsername = "other-user";
    private const string OtherPassword = "OtherUserPassword1234!@#";
    private const string ValidPassword = "ValidPassword1234!@#$";

    // ReSharper disable once ConvertToPrimaryConstructor
    public VoteIntegrityTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    private async Task<Guid> SeedVotingOptionAsync(string name)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var option = new VotingOption { Id = Guid.NewGuid(), Name = name, CreatedAt = DateTime.UtcNow };
        db.VotingOptions.Add(option);
        await db.SaveChangesAsync();
        return option.Id;
    }

    private async Task<Guid> SeedOtherUserAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = OtherUsername,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(OtherPassword),
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<Guid> GetSeededUserIdAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        return (await db.Users.SingleAsync(u => u.Username == UserUsername)).Id;
    }

    private async Task<List<Vote>> GetVotesAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        return await db.Votes.AsNoTracking().ToListAsync();
    }

    private static MultipartFormDataContent BuildEmailWorkbook(params string[] emails)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell(1, 1).SetValue("email");
        for (var i = 0; i < emails.Length; i++)
        {
            sheet.Cell(i + 2, 1).SetValue(emails[i]);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = new ByteArrayContent(stream.ToArray());
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        return new MultipartFormDataContent { { content, "file", "emails.xlsx" } };
    }

    // --- Nobody votes on someone else's behalf ---

    [Fact]
    public async Task CastVote_WithAnotherUsersIdInBody_OnlyRecordsTheCallersVote()
    {
        var optionId = await SeedVotingOptionAsync("Option A");
        var otherUserId = await SeedOtherUserAsync();
        var userId = await GetSeededUserIdAsync();
        var userClient = await CreateAuthenticatedUserClientAsync();

        var response = await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId, userId = otherUserId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var vote = Assert.Single(await GetVotesAsync());
        Assert.Equal(userId, vote.UserId);
    }

    [Fact]
    public async Task GetMyVote_NeverReturnsAnotherUsersVote()
    {
        var optionId = await SeedVotingOptionAsync("Option A");
        await SeedOtherUserAsync();
        var otherClient = await CreateAuthenticatedUserClientAsync(OtherUsername, OtherPassword);
        (await otherClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId })).EnsureSuccessStatusCode();

        var userClient = await CreateAuthenticatedUserClientAsync();
        var mine = await userClient.GetFromJsonAsync<MyVoteResponseDto>($"/api/votes/me?userId={Guid.NewGuid()}");

        Assert.False(mine!.HasVoted);
    }

    // --- One account per email (duplicate accounts would mean duplicate votes) ---

    [Fact]
    public async Task CreateUser_WithExistingEmailInDifferentCase_ReturnsConflict()
    {
        var admin = await CreateAuthenticatedClientAsync();

        var response = await admin.PostAsJsonAsync("/api/users",
            new { username = "duplicate", email = UserEmail.ToUpperInvariant(), password = ValidPassword });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_ToExistingEmailInDifferentCase_ReturnsConflict()
    {
        var otherUserId = await SeedOtherUserAsync();
        var admin = await CreateAuthenticatedClientAsync();

        var response = await admin.PutAsJsonAsync($"/api/users/{otherUserId}",
            new { username = OtherUsername, email = UserEmail.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task BulkImport_WithExistingEmailInDifferentCase_SkipsIt()
    {
        var admin = await CreateAuthenticatedClientAsync();

        var response = await admin.PostAsync("/api/users/bulk-import", BuildEmailWorkbook(UserEmail.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();
        Assert.Empty(body!.Created);
        Assert.Equal("Already exists", Assert.Single(body.Skipped).Reason);
    }

    [Fact]
    public async Task CreateAdministrator_WithExistingEmailInDifferentCase_ReturnsConflict()
    {
        var admin = await CreateAuthenticatedClientAsync();

        var response = await admin.PostAsJsonAsync("/api/administrators",
            new { username = "second-admin", email = AdminEmail.ToUpperInvariant(), password = ValidPassword });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // --- Deleting things never leaves votes behind ---

    [Fact]
    public async Task DeletingAUser_RemovesTheirVote_AndStatsFollow()
    {
        var optionId = await SeedVotingOptionAsync("Option A");
        var userClient = await CreateAuthenticatedUserClientAsync();
        (await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId })).EnsureSuccessStatusCode();
        var admin = await CreateAuthenticatedClientAsync();

        (await admin.DeleteAsync($"/api/users/{await GetSeededUserIdAsync()}")).EnsureSuccessStatusCode();

        Assert.Empty(await GetVotesAsync());
        var stats = await admin.GetFromJsonAsync<VoteStatsDto>("/api/votes/stats");
        Assert.Equal(0, stats!.VotedUsers);
        Assert.Equal(0, Assert.Single(stats.OptionCounts).Count);
    }

    [Fact]
    public async Task DeletingAllUsers_RemovesAllVotes()
    {
        var optionId = await SeedVotingOptionAsync("Option A");
        await SeedOtherUserAsync();
        foreach (var client in new[]
                 {
                     await CreateAuthenticatedUserClientAsync(),
                     await CreateAuthenticatedUserClientAsync(OtherUsername, OtherPassword)
                 })
        {
            (await client.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId })).EnsureSuccessStatusCode();
        }
        var admin = await CreateAuthenticatedClientAsync();

        (await admin.DeleteAsync("/api/users")).EnsureSuccessStatusCode();

        Assert.Empty(await GetVotesAsync());
    }

    [Fact]
    public async Task DeletingAVotingOption_RemovesItsVotes_AndVoterCanVoteAgain()
    {
        var removedId = await SeedVotingOptionAsync("Removed");
        var keptId = await SeedVotingOptionAsync("Kept");
        var userClient = await CreateAuthenticatedUserClientAsync();
        (await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = removedId })).EnsureSuccessStatusCode();
        var admin = await CreateAuthenticatedClientAsync();

        (await admin.DeleteAsync($"/api/voting-options/{removedId}")).EnsureSuccessStatusCode();

        Assert.Empty(await GetVotesAsync());
        var mine = await userClient.GetFromJsonAsync<MyVoteResponseDto>("/api/votes/me");
        Assert.False(mine!.HasVoted);

        var revote = await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = keptId });
        Assert.Equal(HttpStatusCode.OK, revote.StatusCode);
        Assert.Equal(keptId, Assert.Single(await GetVotesAsync()).VotingOptionId);
    }

    [Fact]
    public async Task DeletingAllVotingOptions_RemovesAllVotes()
    {
        var optionId = await SeedVotingOptionAsync("Option A");
        var userClient = await CreateAuthenticatedUserClientAsync();
        (await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId })).EnsureSuccessStatusCode();
        var admin = await CreateAuthenticatedClientAsync();

        (await admin.DeleteAsync("/api/voting-options")).EnsureSuccessStatusCode();

        Assert.Empty(await GetVotesAsync());
        var stats = await admin.GetFromJsonAsync<VoteStatsDto>("/api/votes/stats");
        Assert.Equal(0, stats!.VotedUsers);
    }

    [Fact]
    public async Task VoteCastWhileVotingClosed_ByUser_IsNotRecorded()
    {
        var optionId = await SeedVotingOptionAsync("Option A");
        var admin = await CreateAuthenticatedClientAsync();
        (await admin.PutAsJsonAsync("/api/settings/voting", new { isVotingOpen = false })).EnsureSuccessStatusCode();
        var userClient = await CreateAuthenticatedUserClientAsync();

        var response = await userClient.PostAsJsonAsync("/api/votes", new { votingOptionId = optionId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await GetVotesAsync());
    }

    // --- No secrets or account hints in responses ---

    private static readonly Regex BcryptHash = new(@"\$2[abxy]?\$\d{2}\$");

    private static async Task AssertNoPasswordHashAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordhash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(BcryptHash, raw);
    }

    [Fact]
    public async Task Responses_NeverContainPasswordHashes()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var userId = await GetSeededUserIdAsync();

        await AssertNoPasswordHashAsync(await admin.GetAsync("/api/users"));
        await AssertNoPasswordHashAsync(await admin.GetAsync($"/api/users/{userId}"));
        await AssertNoPasswordHashAsync(await admin.PutAsJsonAsync($"/api/users/{userId}",
            new { username = UserUsername, email = UserEmail, password = ValidPassword }));
        await AssertNoPasswordHashAsync(await admin.PostAsJsonAsync("/api/users", new { username = "fresh-user", password = ValidPassword }));
        await AssertNoPasswordHashAsync(await admin.GetAsync("/api/administrators"));
        await AssertNoPasswordHashAsync(await admin.PostAsJsonAsync("/api/administrators",
            new { username = "fresh-admin", email = "fresh-admin@example.com", password = ValidPassword }));
        await AssertNoPasswordHashAsync(await admin.PutAsJsonAsync("/api/administrators/me/password", new { password = ValidPassword }));
        await AssertNoPasswordHashAsync(await admin.GetAsync("/api/votes"));
        await AssertNoPasswordHashAsync(await Factory.CreateClient().PostAsJsonAsync("/api/auth/admin/login",
            new { username = AdminEmail, password = ValidPassword }));
        await AssertNoPasswordHashAsync(await Factory.CreateClient().PostAsJsonAsync("/api/auth/user/login",
            new { username = UserUsername, password = ValidPassword }));
    }

    [Theory]
    [InlineData("/api/auth/admin/login", AdminEmail)]
    [InlineData("/api/auth/user/login", UserUsername)]
    public async Task Login_UnknownUserAndWrongPassword_AreIndistinguishable(string path, string existingUsername)
    {
        var client = Factory.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync(path, new { username = existingUsername, password = "Wrong-password-1234567!" });
        var unknownUser = await client.PostAsJsonAsync(path, new { username = "nobody-here", password = "Wrong-password-1234567!" });

        Assert.Equal(wrongPassword.StatusCode, unknownUser.StatusCode);
        Assert.Equal(
            (await wrongPassword.Content.ReadFromJsonAsync<MessageDto>())!.Message,
            (await unknownUser.Content.ReadFromJsonAsync<MessageDto>())!.Message);
    }

    // --- Import result files can't smuggle spreadsheet formulas ---

    [Fact]
    public async Task BulkImport_ResultWorkbook_StoresEverythingAsTextWithoutFormulas()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var upload = BuildEmailWorkbook(
            "=HYPERLINK(\"https://evil.example\",\"Click\")",
            "+cmd|' /C calc'!A0",
            "@SUM(1+1)",
            "valid.person@example.com");

        var response = await admin.PostAsync("/api/users/bulk-import", upload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();
        Assert.Single(body!.Created);
        Assert.Equal(3, body.Skipped.Count);

        using var workbook = new XLWorkbook(new MemoryStream(Convert.FromBase64String(body.File!)));
        var cells = workbook.Worksheets.SelectMany(s => s.CellsUsed()).ToList();
        Assert.NotEmpty(cells);
        Assert.All(cells, c =>
        {
            Assert.False(c.HasFormula, $"{c.Worksheet.Name}!{c.Address} holds a formula");
            Assert.True(c.Value.IsText, $"{c.Worksheet.Name}!{c.Address} is not text");
        });
    }
}
