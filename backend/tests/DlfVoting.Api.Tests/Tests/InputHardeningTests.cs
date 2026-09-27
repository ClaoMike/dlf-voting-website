using System.Net;
using System.Net.Http.Json;
using System.Text;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable ClassNeverInstantiated.Local
// ReSharper disable NotAccessedPositionalProperty.Local

namespace DlfVoting.Api.Tests.Tests;

/// <summary>Malformed, missing, oversized and hostile input must come back as a 4xx, never a crash.</summary>
public class InputHardeningTests : IntegrationTestBase
{
    private record VotingOptionResponseDto(Guid Id, string Name, DateTime CreatedAt);
    private record PagedDto(List<object> Items, int TotalCount, int Page, int PageSize);

    private const string ValidPassword = "ValidPassword1234!@#$";

    // ReSharper disable once ConvertToPrimaryConstructor
    public InputHardeningTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    // --- Missing or malformed bodies ---

    [Theory]
    [InlineData("/api/auth/admin/login", "{}")]
    [InlineData("/api/auth/admin/login", "{\"username\":null,\"password\":null}")]
    [InlineData("/api/auth/admin/login", "{\"username\":\"x\"")]
    [InlineData("/api/auth/admin/login", "null")]
    [InlineData("/api/auth/user/login", "{}")]
    [InlineData("/api/auth/user/login", "{\"username\":null,\"password\":null}")]
    [InlineData("/api/auth/user/login", "{\"username\":\"x\"")]
    [InlineData("/api/auth/user/login", "null")]
    public async Task Login_WithMissingOrMalformedBody_ReturnsBadRequestWithoutCookie(string path, string body)
    {
        var response = await Factory.CreateClient().PostAsync(path, Json(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [InlineData("POST", "/api/voting-options", "{}")]
    [InlineData("POST", "/api/voting-options", "{\"name\":null}")]
    [InlineData("POST", "/api/users", "{\"username\":\"valid-user\"}")]
    [InlineData("POST", "/api/administrators", "{\"username\":\"new-admin\",\"password\":\"ValidPassword1234!@#$\"}")]
    [InlineData("PUT", "/api/administrators/me/password", "{}")]
    [InlineData("PUT", "/api/votes/00000000-0000-0000-0000-000000000000", "{\"votingOptionId\":\"not-a-guid\"}")]
    [InlineData("PUT", "/api/settings/voting", "{\"isVotingOpen\":\"maybe\"}")]
    public async Task AdminWrite_WithMissingRequiredFieldOrWrongType_ReturnsBadRequest(string method, string url, string body)
    {
        var admin = await CreateAuthenticatedClientAsync();

        var response = await admin.SendAsync(new HttpRequestMessage(new HttpMethod(method), url) { Content = Json(body) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"isVotingOpen\":null}")]
    public async Task UpdateVotingStatus_WithoutAValue_ReturnsBadRequest_AndLeavesVotingOpen(string body)
    {
        var admin = await CreateAuthenticatedClientAsync();

        var response = await admin.PutAsync("/api/settings/voting", Json(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var status = await admin.GetFromJsonAsync<Dictionary<string, bool>>("/api/settings/voting");
        Assert.True(status!["isVotingOpen"]);
    }

    [Fact]
    public async Task UpdateVotingOption_WithNullName_ReturnsBadRequest()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var created = await admin.PostAsJsonAsync("/api/voting-options", new { name = "Option A" });
        var option = await created.Content.ReadFromJsonAsync<VotingOptionResponseDto>();

        var response = await admin.PutAsync($"/api/voting-options/{option!.Id}", Json("{\"name\":null}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("{\"votingOptionId\":\"not-a-guid\"}")]
    [InlineData("{\"votingOptionId\":12345}")]
    public async Task CastVote_WithMalformedOptionId_ReturnsBadRequest(string body)
    {
        var userClient = await CreateAuthenticatedUserClientAsync();

        var response = await userClient.PostAsync("/api/votes", Json(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CastVote_WithoutOptionId_RecordsNoVote()
    {
        var userClient = await CreateAuthenticatedUserClientAsync();

        var response = await userClient.PostAsync("/api/votes", Json("{}"));

        Assert.False(response.IsSuccessStatusCode);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        Assert.Equal(0, await db.Votes.CountAsync());
    }

    // --- Values longer than the database columns ---

    [Fact]
    public async Task CreateVotingOption_WithNameOf200Characters_Succeeds()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var response = await admin.PostAsJsonAsync("/api/voting-options", new { name = new string('a', 200) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateVotingOption_WithNameLongerThan200Characters_ReturnsBadRequest()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var response = await admin.PostAsJsonAsync("/api/voting-options", new { name = new string('a', 201) });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateVotingOption_WithNameLongerThan200Characters_ReturnsBadRequest()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var created = await admin.PostAsJsonAsync("/api/voting-options", new { name = "Option A" });
        var option = await created.Content.ReadFromJsonAsync<VotingOptionResponseDto>();

        var response = await admin.PutAsJsonAsync($"/api/voting-options/{option!.Id}", new { name = new string('a', 201) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // 321 characters: passes the email regex but exceeds the varchar(320) column.
    private static readonly string TooLongEmail = new string('a', 309) + "@example.com";

    [Fact]
    public async Task CreateUser_WithEmailLongerThan320Characters_ReturnsBadRequest()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var response = await admin.PostAsJsonAsync("/api/users", new { username = "long-email", email = TooLongEmail, password = ValidPassword });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAdministrator_WithEmailLongerThan320Characters_ReturnsBadRequest()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var response = await admin.PostAsJsonAsync("/api/administrators", new { username = "long-email", email = TooLongEmail, password = ValidPassword });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAdministrator_WithUsernameLongerThan320Characters_ReturnsBadRequest()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var response = await admin.PostAsJsonAsync("/api/administrators",
            new { username = new string('a', 321), email = "new-admin@example.com", password = ValidPassword });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithPasswordLongerThan64Characters_ReturnsBadRequest()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var response = await admin.PostAsJsonAsync("/api/users", new { username = "long-pass", password = ValidPassword + new string('a', 50) });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/auth/admin/login")]
    [InlineData("/api/auth/user/login")]
    public async Task Login_WithHugeUsernameAndPassword_ReturnsUnauthorized(string path)
    {
        var response = await Factory.CreateClient().PostAsJsonAsync(path,
            new { username = new string('a', 100_000), password = new string('b', 100_000) });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/auth/user/login", UserUsername, UserPassword)]
    [InlineData("/api/auth/admin/login", AdminEmail, AdminPassword)]
    public async Task Login_WithCorrectPasswordPlusExtraCharacters_ReturnsUnauthorized(string path, string username, string password)
    {
        var response = await Factory.CreateClient().PostAsJsonAsync(path, new { username, password = password + "X" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/auth/user/login", UserUsername, UserPassword)]
    [InlineData("/api/auth/admin/login", AdminEmail, AdminPassword)]
    public async Task Login_WithPasswordInDifferentCase_ReturnsUnauthorized(string path, string username, string password)
    {
        var response = await Factory.CreateClient().PostAsJsonAsync(path, new { username, password = password.ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/auth/user/login", UserUsername)]
    [InlineData("/api/auth/admin/login", AdminEmail)]
    public async Task Login_WithEmptyPassword_IsRejectedWithoutCookie(string path, string username)
    {
        var response = await Factory.CreateClient().PostAsJsonAsync(path, new { username, password = "" });
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    // --- Paging parameters ---

    [Theory]
    [InlineData("/api/users")]
    [InlineData("/api/administrators")]
    [InlineData("/api/votes")]
    public async Task GetPage_WithZeroOrNegativePage_FallsBackToFirstPage(string url)
    {
        var admin = await CreateAuthenticatedClientAsync();

        foreach (var page in new[] { 0, -1, int.MinValue })
        {
            var response = await admin.GetAsync($"{url}?page={page}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<PagedDto>();
            Assert.Equal(1, body!.Page);
            Assert.NotEmpty(body.Items);
        }
    }

    [Theory]
    [InlineData("/api/users")]
    [InlineData("/api/administrators")]
    [InlineData("/api/votes")]
    public async Task GetPage_WithHugePageNumber_ReturnsEmptyPageInsteadOfError(string url)
    {
        var admin = await CreateAuthenticatedClientAsync();

        foreach (var page in new[] { 100_000_000, int.MaxValue })
        {
            var response = await admin.GetAsync($"{url}?page={page}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<PagedDto>();
            Assert.Empty(body!.Items);
        }
    }

    [Fact]
    public async Task GetPage_WithNonNumericPage_ReturnsBadRequest()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var response = await admin.GetAsync("/api/users?page=abc");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Injection-style values are treated as plain data ---

    [Theory]
    [InlineData("/api/auth/user/login")]
    [InlineData("/api/auth/admin/login")]
    public async Task Login_WithSqlInjectionUsername_ReturnsUnauthorized(string path)
    {
        var response = await Factory.CreateClient().PostAsJsonAsync(path, new { username = "' OR '1'='1' --", password = "' OR '1'='1' --" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task VotingOptionName_WithSqlAndScriptPayload_IsStoredVerbatimAndTablesSurvive()
    {
        const string name = "Robert'); DROP TABLE \"Votes\";-- <script>alert(1)</script>";
        var admin = await CreateAuthenticatedClientAsync();

        var create = await admin.PostAsJsonAsync("/api/voting-options", new { name });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var list = await admin.GetFromJsonAsync<List<VotingOptionResponseDto>>("/api/voting-options");
        Assert.Equal(name, Assert.Single(list!).Name);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        Assert.Equal(0, await db.Votes.CountAsync());
    }

    [Fact]
    public async Task Username_WithControlCharacters_IsRejected()
    {
        var admin = await CreateAuthenticatedClientAsync();

        foreach (var username in new[] { "evil\u0000user", "evil\tuser", "evil​user", "evil\nuser" })
        {
            var response = await admin.PostAsJsonAsync("/api/users", new { username, password = ValidPassword });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    // --- Non-development surface ---

    [Fact]
    public async Task OpenApiDocument_IsNotExposedOutsideDevelopment()
    {
        var response = await Factory.CreateClient().GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
