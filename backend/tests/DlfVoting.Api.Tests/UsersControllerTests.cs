using System.Net;
using System.Net.Http.Json;

namespace DlfVoting.Api.Tests;

public class UsersControllerTests : IntegrationTestBase
{
    private record UserResponseDto(Guid Id, string Email, DateTime CreatedAt);
    private record PagedUsersResponseDto(List<UserResponseDto> Items, int TotalCount, int Page, int PageSize);

    private const string ValidPassword = "ValidPassword1234!@#$";

    public UsersControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    private static async Task<Guid> CreateUserAsync(HttpClient client, string email, string password = ValidPassword)
    {
        var response = await client.PostAsJsonAsync("/api/users", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<UserResponseDto>();
        return body!.Id;
    }

    // --- Auth ---

    [Fact]
    public async Task GetPage_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/users", new { email = "a@b.com", password = ValidPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}", new { email = "a@b.com" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.DeleteAsync($"/api/users/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAll_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.DeleteAsync("/api/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithInvalidEmail_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/users", new { email = "not-an-email", password = ValidPassword });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Short1!")]                     // too short
    [InlineData("nouppercasehere1234567!@#")]    // no uppercase
    [InlineData("NoDigitsHereAtAllForSure!@#")]  // no digit
    [InlineData("NoSpecialCharacters12345678")]  // no special char
    public async Task Create_WithInvalidPassword_ReturnsBadRequest(string password)
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/users", new { email = "valid@example.com", password });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithValidData_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/users", new { email = "newuser@example.com", password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UserResponseDto>();
        Assert.Equal("newuser@example.com", body!.Email);
    }

    [Fact]
    public async Task Create_WithDuplicateEmail_ReturnsConflict()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateUserAsync(client, "dup@example.com");

        var response = await client.PostAsJsonAsync("/api/users", new { email = "dup@example.com", password = ValidPassword });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // --- Pagination ---

    [Fact]
    public async Task GetPage_ReturnsUpTo25ItemsSortedByEmail()
    {
        var client = await CreateAuthenticatedClientAsync();
        for (var i = 0; i < 30; i++)
        {
            await CreateUserAsync(client, $"user{i:D2}@example.com");
        }

        var response = await client.GetAsync("/api/users?page=1");
        var body = await response.Content.ReadFromJsonAsync<PagedUsersResponseDto>();

        Assert.Equal(30, body!.TotalCount);
        Assert.Equal(25, body.PageSize);
        Assert.Equal(25, body.Items.Count);

        var emails = body.Items.Select(u => u.Email).ToList();
        var expected = emails.OrderBy(e => e, StringComparer.Ordinal).ToList();
        Assert.Equal(expected, emails);
    }

    [Fact]
    public async Task GetPage_SecondPage_ReturnsRemainingItems()
    {
        var client = await CreateAuthenticatedClientAsync();
        for (var i = 0; i < 30; i++)
        {
            await CreateUserAsync(client, $"user{i:D2}@example.com");
        }

        var response = await client.GetAsync("/api/users?page=2");
        var body = await response.Content.ReadFromJsonAsync<PagedUsersResponseDto>();

        Assert.Equal(2, body!.Page);
        Assert.Equal(5, body.Items.Count);
    }

    // --- Update ---

    [Fact]
    public async Task Update_EmailOnly_UpdatesEmailAndKeepsWorking()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "old@example.com");

        var response = await client.PutAsJsonAsync($"/api/users/{id}", new { email = "new@example.com" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UserResponseDto>();
        Assert.Equal("new@example.com", body!.Email);
    }

    [Fact]
    public async Task Update_PasswordOnly_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "passonly@example.com");

        var response = await client.PutAsJsonAsync($"/api/users/{id}", new { password = "NewValidPassword1234!@#" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_BothFields_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "both@example.com");

        var response = await client.PutAsJsonAsync($"/api/users/{id}", new
        {
            email = "bothnew@example.com",
            password = "AnotherValidPassword1!@#"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithNeitherFieldProvided_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "neither@example.com");

        var response = await client.PutAsJsonAsync($"/api/users/{id}", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithInvalidEmail_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "invalidemailtest@example.com");

        var response = await client.PutAsJsonAsync($"/api/users/{id}", new { email = "not-an-email" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithInvalidPassword_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "invalidpasstest@example.com");

        var response = await client.PutAsJsonAsync($"/api/users/{id}", new { password = "tooshort" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_NonexistentId_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}", new { email = "whoever@example.com" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ToEmailTakenByAnotherUser_ReturnsConflict()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateUserAsync(client, "taken@example.com");
        var id2 = await CreateUserAsync(client, "other@example.com");

        var response = await client.PutAsJsonAsync($"/api/users/{id2}", new { email = "taken@example.com" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_ExistingUser_RemovesFromList()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "todelete@example.com");

        var deleteResponse = await client.DeleteAsync($"/api/users/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/users");
        var body = await listResponse.Content.ReadFromJsonAsync<PagedUsersResponseDto>();
        Assert.DoesNotContain(body!.Items, u => u.Id == id);
    }

    [Fact]
    public async Task Delete_NonexistentId_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.DeleteAsync($"/api/users/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AlreadyDeleted_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "deletetwice@example.com");
        await client.DeleteAsync($"/api/users/{id}");

        var second = await client.DeleteAsync($"/api/users/{id}");
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    // --- Delete all ---

    [Fact]
    public async Task DeleteAll_RemovesEveryUser()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateUserAsync(client, "a@example.com");
        await CreateUserAsync(client, "b@example.com");

        var response = await client.DeleteAsync("/api/users");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var listResponse = await client.GetAsync("/api/users");
        var body = await listResponse.Content.ReadFromJsonAsync<PagedUsersResponseDto>();
        Assert.Empty(body!.Items);
    }

    [Fact]
    public async Task DeleteAll_WithNoUsers_StillReturnsNoContent()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.DeleteAsync("/api/users");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // --- Concurrency ---

    [Fact]
    public async Task ConcurrentCreate_SameEmail_ExactlyOneSucceeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        const string email = "race-create@example.com";

        var task1 = client.PostAsJsonAsync("/api/users", new { email, password = ValidPassword });
        var task2 = client.PostAsJsonAsync("/api/users", new { email, password = ValidPassword });
        var responses = await Task.WhenAll(task1, task2);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task ConcurrentUpdate_DifferentUsersToSameEmail_ExactlyOneSucceeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id1 = await CreateUserAsync(client, "race1@example.com");
        var id2 = await CreateUserAsync(client, "race2@example.com");
        const string targetEmail = "race-contested@example.com";

        var task1 = client.PutAsJsonAsync($"/api/users/{id1}", new { email = targetEmail });
        var task2 = client.PutAsJsonAsync($"/api/users/{id2}", new { email = targetEmail });
        var responses = await Task.WhenAll(task1, task2);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task ConcurrentDelete_SameUser_ExactlyOneSucceeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "race-delete@example.com");

        var task1 = client.DeleteAsync($"/api/users/{id}");
        var task2 = client.DeleteAsync($"/api/users/{id}");
        var responses = await Task.WhenAll(task1, task2);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.NoContent));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task ConcurrentUpdateAndDelete_SameUser_NeverLeavesStaleData()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "race-update-delete@example.com");

        var updateTask = client.PutAsJsonAsync($"/api/users/{id}", new { email = "updated@example.com" });
        var deleteTask = client.DeleteAsync($"/api/users/{id}");
        var updateResponse = await updateTask;
        var deleteResponse = await deleteTask;

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.True(
            updateResponse.StatusCode == HttpStatusCode.OK ||
            updateResponse.StatusCode == HttpStatusCode.NotFound);

        var listResponse = await client.GetAsync("/api/users");
        var body = await listResponse.Content.ReadFromJsonAsync<PagedUsersResponseDto>();
        Assert.DoesNotContain(body!.Items, u => u.Id == id);
    }

    [Fact]
    public async Task ConcurrentDeleteAll_BothCallsSucceed()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateUserAsync(client, "bulk1@example.com");
        await CreateUserAsync(client, "bulk2@example.com");

        var task1 = client.DeleteAsync("/api/users");
        var task2 = client.DeleteAsync("/api/users");
        var responses = await Task.WhenAll(task1, task2);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));

        var listResponse = await client.GetAsync("/api/users");
        var body = await listResponse.Content.ReadFromJsonAsync<PagedUsersResponseDto>();
        Assert.Empty(body!.Items);
    }

    [Fact]
    public async Task ConcurrentDeleteAllAndCreate_LeavesValidEndState()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateUserAsync(client, "existing@example.com");

        var deleteAllTask = client.DeleteAsync("/api/users");
        var createTask = client.PostAsJsonAsync("/api/users", new { email = "brandnew@example.com", password = ValidPassword });
        var deleteResponse = await deleteAllTask;
        var createResponse = await createTask;

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/users");
        var body = await listResponse.Content.ReadFromJsonAsync<PagedUsersResponseDto>();

        Assert.True(
            body!.Items.Count == 0 || (body.Items.Count == 1 && body.Items[0].Email == "brandnew@example.com"),
            $"Unexpected state: {string.Join(", ", body.Items.Select(u => u.Email))}");
    }
}