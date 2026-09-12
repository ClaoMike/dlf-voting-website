using System.Net;
using System.Net.Http.Json;
using System.Text;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

        // +1 accounts for the base seeded test user (UserEmail) created in IntegrationTestBase.
        Assert.Equal(31, body!.TotalCount);
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
        // 31 total, 25 on page 1 → 6 remain on page 2 (30 created + 1 base seeded user).
        Assert.Equal(6, body.Items.Count);
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
    
    // --- Bulk import ---

    private static HttpContent BuildCsvFileContent(string csvContent)
    {
        var multipart = new MultipartFormDataContent();
        var byteContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
        byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        multipart.Add(byteContent, "file", "users.csv");
        return multipart;
    }

    private record BulkImportedUserDto(string Email, string Password);
    private record BulkImportSkippedEntryDto(string Email, string Reason);
    private record BulkImportResponseDto(List<BulkImportedUserDto> Created, List<BulkImportSkippedEntryDto> Skipped);

    [Fact]
    public async Task BulkImport_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var content = BuildCsvFileContent("email\na@example.com");

        var response = await client.PostAsync("/api/users/bulk-import", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BulkImport_WithNoFile_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = new MultipartFormDataContent();

        var response = await client.PostAsync("/api/users/bulk-import", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkImport_WithValidEmails_CreatesAllUsersWithGeneratedPasswords()
    {
        var client = await CreateAuthenticatedClientAsync();
        var csv = "email\nbulk1@example.com\nbulk2@example.com\nbulk3@example.com";
        var content = BuildCsvFileContent(csv);

        var response = await client.PostAsync("/api/users/bulk-import", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();

        Assert.Equal(3, body!.Created.Count);
        Assert.Empty(body.Skipped);

        foreach (var created in body.Created)
        {
            Assert.True(created.Password.Length >= 20);
        }

        // Confirm the returned passwords actually work for login.
        foreach (var created in body.Created)
        {
            var loginClient = Factory.CreateClient();
            var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/user/login", new
            {
                email = created.Email,
                password = created.Password
            });
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        }
    }

    [Fact]
    public async Task BulkImport_WithoutHeaderRow_StillWorks()
    {
        var client = await CreateAuthenticatedClientAsync();
        var csv = "noheader1@example.com\nnoheader2@example.com";
        var content = BuildCsvFileContent(csv);

        var response = await client.PostAsync("/api/users/bulk-import", content);
        var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();

        Assert.Equal(2, body!.Created.Count);
    }

    [Fact]
    public async Task BulkImport_WithDuplicateEmailWithinFile_CreatesOneAndSkipsRest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var csv = "email\ndup-in-file@example.com\ndup-in-file@example.com\ndup-in-file@example.com";
        var content = BuildCsvFileContent(csv);

        var response = await client.PostAsync("/api/users/bulk-import", content);
        var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();

        Assert.Single(body!.Created, c => c.Email == "dup-in-file@example.com");
        Assert.Equal(2, body.Skipped.Count(s => s.Email == "dup-in-file@example.com" && s.Reason == "Duplicate in file"));
    }

    [Fact]
    public async Task BulkImport_WithEmailAlreadyInDatabase_SkipsItWithCorrectReason()
    {
        var client = await CreateAuthenticatedClientAsync();
        await client.PostAsJsonAsync("/api/users", new { email = "already-exists@example.com", password = "SomeValidPassword1!@#" });

        var csv = "email\nalready-exists@example.com\nbrand-new@example.com";
        var content = BuildCsvFileContent(csv);

        var response = await client.PostAsync("/api/users/bulk-import", content);
        var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();

        Assert.Single(body!.Created, c => c.Email == "brand-new@example.com");
        Assert.Single(body.Skipped, s => s.Email == "already-exists@example.com" && s.Reason == "Already exists");
    }

    [Fact]
    public async Task BulkImport_WithInvalidEmailFormat_SkipsItWithCorrectReason()
    {
        var client = await CreateAuthenticatedClientAsync();
        var csv = "email\nnot-an-email\nvalid-one@example.com";
        var content = BuildCsvFileContent(csv);

        var response = await client.PostAsync("/api/users/bulk-import", content);
        var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();

        Assert.Single(body!.Created, c => c.Email == "valid-one@example.com");
        Assert.Single(body.Skipped, s => s.Email == "not-an-email" && s.Reason == "Invalid email format");
    }

    [Fact]
    public async Task BulkImport_WithBlankLinesInFile_IgnoresThem()
    {
        var client = await CreateAuthenticatedClientAsync();
        var csv = "email\n\nblank-line-test@example.com\n\n";
        var content = BuildCsvFileContent(csv);

        var response = await client.PostAsync("/api/users/bulk-import", content);
        var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();

        Assert.Single(body!.Created);
        Assert.Empty(body.Skipped);
    }

    [Fact]
    public async Task BulkImport_MixOfValidDuplicateAndInvalid_HandlesEachCorrectly()
    {
        var client = await CreateAuthenticatedClientAsync();
        await client.PostAsJsonAsync("/api/users", new { email = "mix-existing@example.com", password = "SomeValidPassword1!@#" });

        var csv = string.Join('\n', new[]
        {
            "email",
            "mix-new-1@example.com",
            "mix-new-2@example.com",
            "mix-existing@example.com",
            "mix-new-1@example.com", // duplicate of a valid new one
            "not-valid-email",
        });
        var content = BuildCsvFileContent(csv);

        var response = await client.PostAsync("/api/users/bulk-import", content);
        var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();

        Assert.Equal(2, body!.Created.Count);
        Assert.Contains(body.Created, c => c.Email == "mix-new-1@example.com");
        Assert.Contains(body.Created, c => c.Email == "mix-new-2@example.com");

        Assert.Equal(3, body.Skipped.Count);
        Assert.Contains(body.Skipped, s => s.Email == "mix-existing@example.com" && s.Reason == "Already exists");
        Assert.Contains(body.Skipped, s => s.Email == "mix-new-1@example.com" && s.Reason == "Duplicate in file");
        Assert.Contains(body.Skipped, s => s.Email == "not-valid-email" && s.Reason == "Invalid email format");
    }

    [Fact]
    public async Task BulkImport_GeneratedPasswords_AreAllDifferentFromEachOther()
    {
        var client = await CreateAuthenticatedClientAsync();
        var csv = "email\nunique-pw-1@example.com\nunique-pw-2@example.com\nunique-pw-3@example.com";
        var content = BuildCsvFileContent(csv);

        var response = await client.PostAsync("/api/users/bulk-import", content);
        var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();

        var distinctPasswords = body!.Created.Select(c => c.Password).Distinct().Count();
        Assert.Equal(body.Created.Count, distinctPasswords);
    }

    // --- Bulk import: concurrency ---

    [Fact]
    public async Task ConcurrentBulkImport_OverlappingFiles_NeverCreatesDuplicateAccountForSameEmail()
    {
        var client = await CreateAuthenticatedClientAsync();

        // Two files share one overlapping email, each also has a unique one of its own.
        var csv1 = "email\nrace-shared@example.com\nrace-file1-only@example.com";
        var csv2 = "email\nrace-shared@example.com\nrace-file2-only@example.com";

        var task1 = client.PostAsync("/api/users/bulk-import", BuildCsvFileContent(csv1));
        var task2 = client.PostAsync("/api/users/bulk-import", BuildCsvFileContent(csv2));
        var responses = await Task.WhenAll(task1, task2);

        Assert.All(responses, r =>
            Assert.True(r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Conflict));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var sharedCount = await db.Users.CountAsync(u => u.Email == "race-shared@example.com");

        // Regardless of how the race resolved, the shared email must exist exactly once.
        Assert.Equal(1, sharedCount);
    }

    [Fact]
    public async Task ConcurrentBulkImport_CompletelyDisjointFiles_BothFullySucceed()
    {
        var client = await CreateAuthenticatedClientAsync();

        var csv1 = "email\ndisjoint-a1@example.com\ndisjoint-a2@example.com";
        var csv2 = "email\ndisjoint-b1@example.com\ndisjoint-b2@example.com";

        var task1 = client.PostAsync("/api/users/bulk-import", BuildCsvFileContent(csv1));
        var task2 = client.PostAsync("/api/users/bulk-import", BuildCsvFileContent(csv2));
        var responses = await Task.WhenAll(task1, task2);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        var body1 = await responses[0].Content.ReadFromJsonAsync<BulkImportResponseDto>();
        var body2 = await responses[1].Content.ReadFromJsonAsync<BulkImportResponseDto>();

        Assert.Equal(2, body1!.Created.Count);
        Assert.Equal(2, body2!.Created.Count);
    }

}