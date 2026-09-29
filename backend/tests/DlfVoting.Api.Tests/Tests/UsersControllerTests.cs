using System.Net;
using System.Net.Http.Json;
using System.Text;
using ClosedXML.Excel;
using DlfVoting.Api.Validation;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable ClassNeverInstantiated.Local
// ReSharper disable NotAccessedPositionalProperty.Local

namespace DlfVoting.Api.Tests.Tests;

public class UsersControllerTests : IntegrationTestBase
{
    private record UserResponseDto(
        Guid Id,
        string Username,
        string? Email,
        string? EmployeeCode,
        string? FirstName,
        string? LastName,
        string? CompanyCode,
        DateOnly? EmploymentDate,
        string? Electability,
        DateTime CreatedAt);
    private record UserListItemDto(Guid Id, string? Name, string Username);
    private record PagedUsersResponseDto(List<UserListItemDto> Items, int TotalCount, int Page, int PageSize);
    private record MessageDto(string Message);

    private const string ValidPassword = "ValidPassword1234!@#$";

    // ReSharper disable once ConvertToPrimaryConstructor
    public UsersControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    private static async Task<Guid> CreateUserAsync(HttpClient client, string username, string? email = null, string password = ValidPassword)
    {
        var response = await client.PostAsJsonAsync("/api/users", new { username, email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<UserResponseDto>();
        return body!.Id;
    }

    private async Task<HttpStatusCode> LoginStatusAsync(string username, string password)
    {
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/auth/user/login", new { username, password });
        return response.StatusCode;
    }

    private static async Task<string?> ReadMessageAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<MessageDto>();
        return body?.Message;
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
        var response = await client.PostAsJsonAsync("/api/users", new { username = "someone", password = ValidPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}", new { username = "someone" });
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
    public async Task Create_WithValidData_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/users", new { username = "newuser", email = "newuser@example.com", password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UserResponseDto>();
        Assert.Equal("newuser", body!.Username);
        Assert.Equal("newuser@example.com", body.Email);
        Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync("newuser", ValidPassword));
    }

    [Fact]
    public async Task Create_WithoutEmail_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/users", new { username = "no-email-user", password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UserResponseDto>();
        Assert.Null(body!.Email);
    }

    [Fact]
    public async Task Create_TwoUsersWithoutEmail_BothSucceed()
    {
        // Email is unique only when present; several users may have none.
        var client = await CreateAuthenticatedClientAsync();
        await CreateUserAsync(client, "no-email-1");
        await CreateUserAsync(client, "no-email-2");
    }

    [Theory]
    [InlineData("abcd")]                  // too short (min 5)
    [InlineData("abcdefghijklmnopqrstu")] // too long (max 20)
    [InlineData("has space")]             // whitespace not allowed
    [InlineData("")]                      // missing
    public async Task Create_WithInvalidUsername_ReturnsBadRequest(string username)
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/users", new { username, password = ValidPassword });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithAtSignAndDanishLettersInUsername_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/users", new { username = "søren@æøå", password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync("SØREN@ÆØÅ", ValidPassword));
    }

    [Fact]
    public async Task Create_WithInvalidEmail_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/users", new { username = "valid-name", email = "not-an-email", password = ValidPassword });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Short1!")]                     // too short (7 characters)
    [InlineData("nouppercase1!")]                // no uppercase
    [InlineData("NoDigits!")]                    // no digit
    [InlineData("NoSpecial1")]                   // no special char
    [InlineData("TooLong1!TooLong1!TooLong1!TooLong1!TooLong1!TooLong1!TooLong1!xy")] // 65 characters
    public async Task Create_WithInvalidPassword_ReturnsBadRequest(string password)
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/users", new { username = "valid-name", password });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(IdentityRules.InvalidUserPasswordMessage, await ReadMessageAsync(response));
    }

    [Fact]
    public async Task Create_WithEightCharacterPassword_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateUserAsync(client, "eight-chars", password: "Eight1!x");

        Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync("eight-chars", "Eight1!x"));
    }

    [Fact]
    public async Task Create_WithGeneratedPassword_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var password = SecurePasswordGenerator.Generate();
        await CreateUserAsync(client, "generated-pw", password: password);

        Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync("generated-pw", password));
    }

    [Fact]
    public async Task Create_WithDuplicateUsernameInDifferentCase_ReturnsConflict()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateUserAsync(client, "dup-user");

        var response = await client.PostAsJsonAsync("/api/users", new { username = "DUP-User", password = ValidPassword });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("A user with this username already exists.", await ReadMessageAsync(response));
    }

    [Fact]
    public async Task Create_WithDuplicateEmail_ReturnsConflict()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateUserAsync(client, "dup-email-1", "dup@example.com");

        var response = await client.PostAsJsonAsync("/api/users", new { username = "dup-email-2", email = "dup@example.com", password = ValidPassword });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("A user with this email already exists.", await ReadMessageAsync(response));
    }

    // --- Pagination ---

    [Fact]
    public async Task GetPage_ReturnsUpTo25Items_UsersWithoutNameSortedByUsername()
    {
        var client = await CreateAuthenticatedClientAsync();
        for (var i = 0; i < 30; i++)
        {
            await CreateUserAsync(client, $"user{i:D2}");
        }

        var response = await client.GetAsync("/api/users?page=1");
        var body = await response.Content.ReadFromJsonAsync<PagedUsersResponseDto>();

        // +1 accounts for the base seeded test user created in IntegrationTestBase.
        Assert.Equal(31, body!.TotalCount);
        Assert.Equal(25, body.PageSize);
        Assert.Equal(25, body.Items.Count);

        var usernames = body.Items.Select(u => u.Username).ToList();
        var expected = usernames.OrderBy(u => u, StringComparer.Ordinal).ToList();
        Assert.Equal(expected, usernames);
    }

    [Fact]
    public async Task GetPage_IsSortedAlphabeticallyByName_WithDanishLettersLast_AndUnnamedUsersAfter()
    {
        var client = await CreateAuthenticatedClientAsync();
        await RunImportAsync(client, "/api/users/import-employees", BuildXlsxFileContent([
            EmployeeHeader,
            ["S1", "Åse", "Berg", "DLF01", null, null],
            ["S2", "Zenia", "Dahl", "DLF01", null, null],
            ["S3", "anders", "Holm", "DLF01", null, null],   // lowercase must not sort after Z
            ["S4", "Ørsted", "Lund", "DLF01", null, null],
            ["S5", "Æbelø", "Kjær", "DLF01", null, null],
            ["S6", "Anders", "Berg", "DLF01", null, null],   // same first name: ordered by last name
        ]));
        await CreateUserAsync(client, "aaaaa-no-name");

        var response = await client.GetAsync("/api/users?page=1");
        var body = await response.Content.ReadFromJsonAsync<PagedUsersResponseDto>();

        // Danish alphabet: ... X, Y, Z, Æ, Ø, Å. Users without a name come last, by username.
        Assert.Equal(
            ["Anders Berg", "anders Holm", "Zenia Dahl", "Æbelø Kjær", "Ørsted Lund", "Åse Berg", null, null],
            body!.Items.Select(u => u.Name).ToList());
        Assert.Equal(["aaaaa-no-name", UserUsername], body.Items.Skip(6).Select(u => u.Username).ToList());
    }

    [Fact]
    public async Task GetPage_ReturnsOnlyIdNameAndUsername()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/users?page=1");
        var json = await response.Content.ReadAsStringAsync();

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var item = document.RootElement.GetProperty("items")[0];
        var fields = item.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(["id", "name", "username"], fields);
    }

    // --- Get by id ---

    [Fact]
    public async Task GetById_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync($"/api/users/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NonexistentId_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.GetAsync($"/api/users/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ReturnsFullUser()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "details-user", "details@example.com");

        var response = await client.GetAsync($"/api/users/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserResponseDto>();
        Assert.Equal("details-user", body!.Username);
        Assert.Equal("details@example.com", body.Email);
    }

    [Fact]
    public async Task GetPage_SecondPage_ReturnsRemainingItems()
    {
        var client = await CreateAuthenticatedClientAsync();
        for (var i = 0; i < 30; i++)
        {
            await CreateUserAsync(client, $"user{i:D2}");
        }

        var response = await client.GetAsync("/api/users?page=2");
        var body = await response.Content.ReadFromJsonAsync<PagedUsersResponseDto>();

        Assert.Equal(2, body!.Page);
        // 31 total, 25 on page 1 → 6 remain on page 2 (30 created + 1 base seeded user).
        Assert.Equal(6, body.Items.Count);
    }

    // --- Update ---

    [Fact]
    public async Task Update_Email_UpdatesEmail()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "email-change", "old@example.com");

        var response = await client.PutAsJsonAsync($"/api/users/{id}", new { username = "email-change", email = "new@example.com" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UserResponseDto>();
        Assert.Equal("new@example.com", body!.Email);
    }

    [Fact]
    public async Task Update_WithEmptyEmail_RemovesEmail()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "email-clear", "clear-me@example.com");

        var response = await client.PutAsJsonAsync($"/api/users/{id}", new { username = "email-clear", email = "" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UserResponseDto>();
        Assert.Null(body!.Email);
    }

    [Fact]
    public async Task Update_Username_AllowsLoginWithNewUsernameOnly()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "before-rename");

        var response = await client.PutAsJsonAsync($"/api/users/{id}", new { username = "after-rename" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync("after-rename", ValidPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatusAsync("before-rename", ValidPassword));
    }

    [Fact]
    public async Task Update_PasswordOnly_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "pass-only");

        var response = await client.PutAsJsonAsync($"/api/users/{id}", new { username = "pass-only", password = "NewValidPassword1234!@#" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync("pass-only", "NewValidPassword1234!@#"));
    }

    [Fact]
    public async Task Update_AllFields_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "all-fields");

        var response = await client.PutAsJsonAsync($"/api/users/{id}", new
        {
            username = "all-fields-new",
            email = "allnew@example.com",
            password = "AnotherValidPassword1!@#"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutUsername_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "no-username");

        var response = await client.PutAsJsonAsync($"/api/users/{id}", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithInvalidEmail_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "bad-email-test");

        var response = await client.PutAsJsonAsync($"/api/users/{id}", new { username = "bad-email-test", email = "not-an-email" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithInvalidPassword_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "bad-pass-test");

        var response = await client.PutAsJsonAsync($"/api/users/{id}", new { username = "bad-pass-test", password = "tooshort" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_ToEightCharacterPassword_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "eight-update");

        var response = await client.PutAsJsonAsync($"/api/users/{id}", new { username = "eight-update", password = "Eight1!x" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync("eight-update", "Eight1!x"));
    }

    [Fact]
    public async Task Update_NonexistentId_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}", new { username = "whoever" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ToUsernameTakenByAnotherUser_ReturnsConflict()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateUserAsync(client, "taken-name");
        var id2 = await CreateUserAsync(client, "other-name");

        var response = await client.PutAsJsonAsync($"/api/users/{id2}", new { username = "TAKEN-NAME" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_ToEmailTakenByAnotherUser_ReturnsConflict()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateUserAsync(client, "taken-email", "taken@example.com");
        var id2 = await CreateUserAsync(client, "other-email", "other@example.com");

        var response = await client.PutAsJsonAsync($"/api/users/{id2}", new { username = "other-email", email = "taken@example.com" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_ExistingUser_RemovesFromList()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "to-delete");

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
        var id = await CreateUserAsync(client, "delete-twice");
        await client.DeleteAsync($"/api/users/{id}");

        var second = await client.DeleteAsync($"/api/users/{id}");
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    // --- Delete all ---

    [Fact]
    public async Task DeleteAll_RemovesEveryUser()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateUserAsync(client, "user-a");
        await CreateUserAsync(client, "user-b");

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
    public async Task ConcurrentCreate_SameUsername_ExactlyOneSucceeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        const string username = "race-create";

        var task1 = client.PostAsJsonAsync("/api/users", new { username, password = ValidPassword });
        var task2 = client.PostAsJsonAsync("/api/users", new { username, password = ValidPassword });
        var responses = await Task.WhenAll(task1, task2);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task ConcurrentUpdate_DifferentUsersToSameUsername_ExactlyOneSucceeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id1 = await CreateUserAsync(client, "race-1");
        var id2 = await CreateUserAsync(client, "race-2");
        const string target = "race-contested";

        var task1 = client.PutAsJsonAsync($"/api/users/{id1}", new { username = target });
        var task2 = client.PutAsJsonAsync($"/api/users/{id2}", new { username = target });
        var responses = await Task.WhenAll(task1, task2);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task ConcurrentUpdate_DifferentUsersToSameEmail_ExactlyOneSucceeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id1 = await CreateUserAsync(client, "email-race-1");
        var id2 = await CreateUserAsync(client, "email-race-2");
        const string targetEmail = "race-contested@example.com";

        var task1 = client.PutAsJsonAsync($"/api/users/{id1}", new { username = "email-race-1", email = targetEmail });
        var task2 = client.PutAsJsonAsync($"/api/users/{id2}", new { username = "email-race-2", email = targetEmail });
        var responses = await Task.WhenAll(task1, task2);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task ConcurrentDelete_SameUser_ExactlyOneSucceeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateUserAsync(client, "race-delete");

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
        var id = await CreateUserAsync(client, "race-upd-delete");

        var updateTask = client.PutAsJsonAsync($"/api/users/{id}", new { username = "race-updated" });
        var deleteTask = client.DeleteAsync($"/api/users/{id}");
        var updateResponse = await updateTask;
        var deleteResponse = await deleteTask;

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.True(updateResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound);

        var listResponse = await client.GetAsync("/api/users");
        var body = await listResponse.Content.ReadFromJsonAsync<PagedUsersResponseDto>();
        Assert.DoesNotContain(body!.Items, u => u.Id == id);
    }

    [Fact]
    public async Task ConcurrentDeleteAll_BothCallsSucceed()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateUserAsync(client, "bulk-1");
        await CreateUserAsync(client, "bulk-2");

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
        await CreateUserAsync(client, "existing");

        var deleteAllTask = client.DeleteAsync("/api/users");
        var createTask = client.PostAsJsonAsync("/api/users", new { username = "brand-new", password = ValidPassword });
        var deleteResponse = await deleteAllTask;
        var createResponse = await createTask;

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/users");
        var body = await listResponse.Content.ReadFromJsonAsync<PagedUsersResponseDto>();

        Assert.True(
            body!.Items is [] or [{ Username: "brand-new" }],
            $"Unexpected state: {string.Join(", ", body.Items.Select(u => u.Username))}");
    }

    // --- Excel helpers ---

    /// <summary>Builds an .xlsx upload; each row's values go in consecutive columns (null = empty cell).</summary>
    private static MultipartFormDataContent BuildXlsxFileContent(IEnumerable<object?[]> rows, string fileName = "users.xlsx")
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");

        var r = 1;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Length; c++)
            {
                if (row[c] is not null)
                {
                    sheet.Cell(r, c + 1).Value = XLCellValue.FromObject(row[c]);
                }
            }
            r++;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return BuildFileContent(stream.ToArray(), fileName,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private static MultipartFormDataContent BuildXlsxFileContent(params string[] firstColumn) =>
        BuildXlsxFileContent(firstColumn.Select(v => new object?[] { v.Length == 0 ? null : v }));

    private static MultipartFormDataContent BuildFileContent(byte[] bytes, string fileName, string contentType)
    {
        var multipart = new MultipartFormDataContent();
        var byteContent = new ByteArrayContent(bytes);
        byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        multipart.Add(byteContent, "file", fileName);
        return multipart;
    }

    private static XLWorkbook OpenResultWorkbook(string base64) =>
        new(new MemoryStream(Convert.FromBase64String(base64)));

    // --- Email import ---

    private record BulkImportedUserDto(string Email, string Username, string Password);
    private record BulkImportSkippedEntryDto(string Email, string Reason);
    private record BulkImportResponseDto(List<BulkImportedUserDto> Created, List<BulkImportSkippedEntryDto> Skipped, string? File);

    [Fact]
    public async Task BulkImport_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await RunImportAsync(client, "/api/users/bulk-import", BuildXlsxFileContent("email", "a@example.com"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BulkImport_WithNoFile_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await RunImportAsync(client, "/api/users/bulk-import", new MultipartFormDataContent());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkImport_WithCsvFile_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var csv = BuildFileContent(Encoding.UTF8.GetBytes("email\ncsv@example.com"), "users.csv", "text/csv");

        var response = await RunImportAsync(client, "/api/users/bulk-import", csv);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Please upload an Excel file (.xlsx).", await ReadMessageAsync(response));
    }

    [Fact]
    public async Task BulkImport_WithCsvRenamedToXlsx_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var fake = BuildFileContent(Encoding.UTF8.GetBytes("email\ncsv@example.com"), "users.xlsx", "text/csv");

        var response = await RunImportAsync(client, "/api/users/bulk-import", fake);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkImport_WithValidEmails_CreatesUsersWithGeneratedUsernamesAndPasswords()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = BuildXlsxFileContent("email", "bulk1@example.com", "bulk2@example.com", "bulk3@example.com");

        var response = await RunImportAsync(client, "/api/users/bulk-import", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();

        Assert.Equal(3, body!.Created.Count);
        Assert.Empty(body.Skipped);
        Assert.Equal(3, body.Created.Select(c => c.Username).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var created in body.Created)
        {
            Assert.InRange(created.Username.Length, 5, 20);
            Assert.Equal(SecurePasswordGenerator.UserPasswordLength, created.Password.Length);
            Assert.True(IdentityRules.IsValidUserPassword(created.Password));
            // The generated credentials must actually work for login.
            Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync(created.Username, created.Password));
        }
    }

    [Fact]
    public async Task BulkImport_GeneratesEightCharacterPasswords_InResponseAndWorkbook()
    {
        var client = await CreateAuthenticatedClientAsync();
        var emails = Enumerable.Range(1, 20).Select(i => $"eight{i}@example.com").ToArray();
        var content = BuildXlsxFileContent(["email", .. emails]);

        var response = await RunImportAsync(client, "/api/users/bulk-import", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();

        Assert.Equal(20, body!.Created.Count);
        Assert.All(body.Created, created => Assert.Equal(8, created.Password.Length));

        // The downloaded workbook is what admins hand out, so it must carry the same 8-character passwords.
        using var workbook = OpenResultWorkbook(body.File!);
        var sheet = workbook.Worksheet("Created users");
        for (var row = 2; row <= 21; row++)
        {
            Assert.Equal(8, sheet.Cell(row, 3).GetString().Length);
        }
    }

    [Fact]
    public async Task BulkImport_ReturnsWorkbookWithCreatedAndSkippedSheets()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = BuildXlsxFileContent("email", "sheet-created@example.com", "not-an-email");

        var response = await RunImportAsync(client, "/api/users/bulk-import", content);
        var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();

        using var workbook = OpenResultWorkbook(body!.File!);
        var createdSheet = workbook.Worksheet("Created users");
        Assert.Equal("Email", createdSheet.Cell(1, 1).GetString());
        Assert.Equal("Username", createdSheet.Cell(1, 2).GetString());
        Assert.Equal("Password", createdSheet.Cell(1, 3).GetString());
        Assert.Equal("sheet-created@example.com", createdSheet.Cell(2, 1).GetString());
        Assert.Equal(body.Created[0].Username, createdSheet.Cell(2, 2).GetString());
        Assert.Equal(body.Created[0].Password, createdSheet.Cell(2, 3).GetString());

        var skippedSheet = workbook.Worksheet("Skipped");
        Assert.Equal("not-an-email", skippedSheet.Cell(2, 1).GetString());
        Assert.Equal("Invalid email format", skippedSheet.Cell(2, 2).GetString());
    }

    [Fact]
    public async Task BulkImport_WithoutHeaderRow_StillWorks()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = BuildXlsxFileContent("noheader1@example.com", "noheader2@example.com");

        var response = await RunImportAsync(client, "/api/users/bulk-import", content);
        var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();

        Assert.Equal(2, body!.Created.Count);
    }

    [Fact]
    public async Task BulkImport_WithDuplicateEmailWithinFile_CreatesOneAndSkipsRest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = BuildXlsxFileContent("email", "dup-in-file@example.com", "dup-in-file@example.com", "dup-in-file@example.com");

        var response = await RunImportAsync(client, "/api/users/bulk-import", content);
        var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();

        Assert.Single(body!.Created, c => c.Email == "dup-in-file@example.com");
        Assert.Equal(2, body.Skipped.Count(s => s is { Email: "dup-in-file@example.com", Reason: "Duplicate in file" }));
    }

    [Fact]
    public async Task BulkImport_WithEmailAlreadyInDatabase_SkipsItWithCorrectReason()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateUserAsync(client, "already-exists", "already-exists@example.com");

        var content = BuildXlsxFileContent("email", "already-exists@example.com", "brand-new@example.com");

        var response = await RunImportAsync(client, "/api/users/bulk-import", content);
        var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();

        Assert.Single(body!.Created, c => c.Email == "brand-new@example.com");
        Assert.Single(body.Skipped, s => s is { Email: "already-exists@example.com", Reason: "Already exists" });
    }

    [Fact]
    public async Task BulkImport_WithInvalidEmailFormat_SkipsItWithCorrectReason()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = BuildXlsxFileContent("email", "not-an-email", "valid-one@example.com");

        var response = await RunImportAsync(client, "/api/users/bulk-import", content);
        var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();

        Assert.Single(body!.Created, c => c.Email == "valid-one@example.com");
        Assert.Single(body.Skipped, s => s is { Email: "not-an-email", Reason: "Invalid email format" });
    }

    [Fact]
    public async Task BulkImport_WithBlankRowsInFile_IgnoresThem()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = BuildXlsxFileContent("email", "", "blank-line-test@example.com", "");

        var response = await RunImportAsync(client, "/api/users/bulk-import", content);
        var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();

        Assert.Single(body!.Created);
        Assert.Empty(body.Skipped);
    }

    [Fact]
    public async Task BulkImport_MixOfValidDuplicateAndInvalid_HandlesEachCorrectly()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateUserAsync(client, "mix-existing", "mix-existing@example.com");

        var content = BuildXlsxFileContent(
            "email",
            "mix-new-1@example.com",
            "mix-new-2@example.com",
            "mix-existing@example.com",
            "mix-new-1@example.com", // duplicate of a valid new one
            "not-valid-email");

        var response = await RunImportAsync(client, "/api/users/bulk-import", content);
        var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();

        Assert.Equal(2, body!.Created.Count);
        Assert.Contains(body.Created, c => c.Email == "mix-new-1@example.com");
        Assert.Contains(body.Created, c => c.Email == "mix-new-2@example.com");

        Assert.Equal(3, body.Skipped.Count);
        Assert.Contains(body.Skipped, s => s is { Email: "mix-existing@example.com", Reason: "Already exists" });
        Assert.Contains(body.Skipped, s => s is { Email: "mix-new-1@example.com", Reason: "Duplicate in file" });
        Assert.Contains(body.Skipped, s => s is { Email: "not-valid-email", Reason: "Invalid email format" });
    }

    [Fact]
    public async Task BulkImport_GeneratedPasswords_AreAllDifferentFromEachOther()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = BuildXlsxFileContent("email", "unique-pw-1@example.com", "unique-pw-2@example.com", "unique-pw-3@example.com");

        var response = await RunImportAsync(client, "/api/users/bulk-import", content);
        var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();

        var distinctPasswords = body!.Created.Select(c => c.Password).Distinct().Count();
        Assert.Equal(body.Created.Count, distinctPasswords);
    }

    // --- Email import: concurrency ---

    [Fact]
    public async Task ConcurrentBulkImport_OverlappingFiles_NeverCreatesDuplicateAccountForSameEmail()
    {
        var client = await CreateAuthenticatedClientAsync();

        // Two files share one overlapping email, each also has a unique one of its own.
        var task1 = RunImportAsync(client, "/api/users/bulk-import",
            BuildXlsxFileContent("email", "race-shared@example.com", "race-file1-only@example.com"));
        var task2 = RunImportAsync(client, "/api/users/bulk-import",
            BuildXlsxFileContent("email", "race-shared@example.com", "race-file2-only@example.com"));
        var responses = await Task.WhenAll(task1, task2);

        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var sharedCount = await db.Users.CountAsync(u => u.Email == "race-shared@example.com");

        // Regardless of how the race resolved, the shared email must exist exactly once.
        Assert.Equal(1, sharedCount);
    }

    [Fact]
    public async Task ConcurrentBulkImport_DisjointFiles_OneAtATime_AndEveryAcceptedImportIsComplete()
    {
        var client = await CreateAuthenticatedClientAsync();

        var task1 = RunImportAsync(client, "/api/users/bulk-import",
            BuildXlsxFileContent("email", "disjoint-a1@example.com", "disjoint-a2@example.com"));
        var task2 = RunImportAsync(client, "/api/users/bulk-import",
            BuildXlsxFileContent("email", "disjoint-b1@example.com", "disjoint-b2@example.com"));
        var responses = await Task.WhenAll(task1, task2);

        // Imports run one at a time: an overlapping one is turned away (409) rather than slowing both down.
        foreach (var response in responses)
        {
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                Assert.Equal("An import is already running. Please wait for it to finish.", await ReadMessageAsync(response));
                continue;
            }

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();
            Assert.Equal(2, body!.Created.Count);
        }

        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);
    }

    // --- Employee import ---

    private record EmployeeImportWarningDto(int Row, string Message);
    private record EmployeeImportResponseDto(int CreatedCount, int SkippedCount, List<EmployeeImportWarningDto> Warnings, string File);

    private static readonly object?[] EmployeeHeader =
        ["Kode", "Fornavn", "Efternavn", "Virksomhedskode", "Ansættelsesdato", "Valgbarhed"];

    private async Task<UserResponseDto> GetUserByEmployeeCodeAsync(HttpClient client, string employeeCode)
    {
        Guid id;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
            id = (await db.Users.SingleAsync(u => u.EmployeeCode == employeeCode)).Id;
        }

        var response = await client.GetAsync($"/api/users/{id}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserResponseDto>())!;
    }

    [Fact]
    public async Task ImportEmployees_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await RunImportAsync(client, "/api/users/import-employees", BuildXlsxFileContent([EmployeeHeader]));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ImportEmployees_WithCsvFile_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var csv = BuildFileContent(Encoding.UTF8.GetBytes("Kode;Fornavn\n1;Søren"), "employees.csv", "text/csv");

        var response = await RunImportAsync(client, "/api/users/import-employees", csv);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ImportEmployees_WithoutExpectedColumns_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = BuildXlsxFileContent([["Name", "Company"], ["Søren", "DLF"]]);

        var response = await RunImportAsync(client, "/api/users/import-employees", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ImportEmployees_StoresEmployeeFieldsIncludingDanishCharacters()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = BuildXlsxFileContent([
            EmployeeHeader,
            ["E1001", "Søren", "Østergård", "DLF01", new DateTime(2019, 3, 1), "Valgbar"],
            ["E1002", "Åse", "Ærø", "DLF02", "15-08-2021", "Ikke valgbar"],
        ]);

        var response = await RunImportAsync(client, "/api/users/import-employees", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EmployeeImportResponseDto>();
        Assert.Equal(2, body!.CreatedCount);
        Assert.Empty(body.Warnings);

        var soren = await GetUserByEmployeeCodeAsync(client, "E1001");
        Assert.Equal("Søren", soren.FirstName);
        Assert.Equal("Østergård", soren.LastName);
        Assert.Equal("DLF01", soren.CompanyCode);
        Assert.Equal(new DateOnly(2019, 3, 1), soren.EmploymentDate); // real Excel date cell
        Assert.Equal("Valgbar", soren.Electability);
        Assert.Null(soren.Email);

        var ase = await GetUserByEmployeeCodeAsync(client, "E1002");
        Assert.Equal(new DateOnly(2021, 8, 15), ase.EmploymentDate); // Danish dd-MM-yyyy typed as text
        Assert.Equal("Ikke valgbar", ase.Electability);
    }

    [Fact]
    public async Task ImportEmployees_ReturnsSameWorkbookWithWorkingUsernameAndPasswordColumns()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = BuildXlsxFileContent([
            EmployeeHeader,
            ["E2001", "Mette", "Nørgaard", "DLF03", null, "Valgbar"],
            ["E2002", "Jørgen", "Hansen", "DLF03", null, null],
        ]);

        var response = await RunImportAsync(client, "/api/users/import-employees", content);
        var body = await response.Content.ReadFromJsonAsync<EmployeeImportResponseDto>();

        using var workbook = OpenResultWorkbook(body!.File);
        var sheet = workbook.Worksheet(1);

        // Original columns untouched, the two new ones appended after them.
        Assert.Equal("Kode", sheet.Cell(1, 1).GetString());
        Assert.Equal("Valgbarhed", sheet.Cell(1, 6).GetString());
        Assert.Equal("Username", sheet.Cell(1, 7).GetString());
        Assert.Equal("Password", sheet.Cell(1, 8).GetString());
        Assert.Equal("Nørgaard", sheet.Cell(2, 3).GetString());

        var usernames = new List<string>();
        for (var row = 2; row <= 3; row++)
        {
            var username = sheet.Cell(row, 7).GetString();
            var password = sheet.Cell(row, 8).GetString();
            Assert.InRange(username.Length, 5, 20);
            Assert.Equal(SecurePasswordGenerator.UserPasswordLength, password.Length);
            Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync(username, password));
            usernames.Add(username);
        }
        Assert.NotEqual(usernames[0], usernames[1]);
    }

    [Fact]
    public async Task ImportEmployees_GeneratesEightCharacterPasswords()
    {
        var client = await CreateAuthenticatedClientAsync();
        object?[][] rows = [EmployeeHeader, .. Enumerable.Range(1, 20).Select(i => new object?[] { $"E8{i:000}", "Anna", $"Test{i}", "DLF01", null, "Valgbar" })];
        var content = BuildXlsxFileContent(rows);

        var response = await RunImportAsync(client, "/api/users/import-employees", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EmployeeImportResponseDto>();
        Assert.Equal(20, body!.CreatedCount);

        using var workbook = OpenResultWorkbook(body.File);
        var sheet = workbook.Worksheet(1);
        for (var row = 2; row <= 21; row++)
        {
            var password = sheet.Cell(row, 8).GetString();
            Assert.Equal(8, password.Length);
            Assert.True(IdentityRules.IsValidUserPassword(password), $"Row {row}: '{password}' does not meet the user password rule.");
        }
    }

    [Fact]
    public async Task ImportEmployees_FindsHeaderBelowTitleRow()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = BuildXlsxFileContent([
            ["Koncernvalg 2026 - medarbejderliste"],
            [],
            EmployeeHeader,
            ["E3001", "Karen", "Blixen", "DLF01", null, null],
        ]);

        var response = await RunImportAsync(client, "/api/users/import-employees", content);
        var body = await response.Content.ReadFromJsonAsync<EmployeeImportResponseDto>();

        Assert.Equal(1, body!.CreatedCount);
        using var workbook = OpenResultWorkbook(body.File);
        Assert.Equal("Username", workbook.Worksheet(1).Cell(3, 7).GetString());
        Assert.NotEmpty(workbook.Worksheet(1).Cell(4, 7).GetString());
    }

    [Fact]
    public async Task ImportEmployees_WithUnrecognisedDate_ImportsRowWithWarning()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = BuildXlsxFileContent([
            EmployeeHeader,
            ["E4001", "Niels", "Bohr", "DLF01", "sometime in 2020", null],
        ]);

        var response = await RunImportAsync(client, "/api/users/import-employees", content);
        var body = await response.Content.ReadFromJsonAsync<EmployeeImportResponseDto>();

        Assert.Equal(1, body!.CreatedCount);
        var warning = Assert.Single(body.Warnings);
        Assert.Equal(2, warning.Row);
        Assert.Contains("sometime in 2020", warning.Message);

        var niels = await GetUserByEmployeeCodeAsync(client, "E4001");
        Assert.Null(niels.EmploymentDate);
    }

    [Fact]
    public async Task ImportEmployees_BlankRows_AreIgnoredAndLeftEmpty()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = BuildXlsxFileContent([
            EmployeeHeader,
            ["E5001", "Tove", "Ditlevsen", "DLF01", null, null],
            [],
            ["E5002", "Inger", "Christensen", "DLF01", null, null],
        ]);

        var response = await RunImportAsync(client, "/api/users/import-employees", content);
        var body = await response.Content.ReadFromJsonAsync<EmployeeImportResponseDto>();

        Assert.Equal(2, body!.CreatedCount);
        Assert.Equal(0, body.SkippedCount);
        using var workbook = OpenResultWorkbook(body.File);
        Assert.True(workbook.Worksheet(1).Cell(3, 7).IsEmpty());
    }

    [Fact]
    public async Task ImportEmployees_WithTooLongValue_SkipsRowWithWarning()
    {
        var client = await CreateAuthenticatedClientAsync();
        var content = BuildXlsxFileContent([
            EmployeeHeader,
            ["E6001", new string('x', 201), "Too Long", "DLF01", null, null],
            ["E6002", "Fine", "Row", "DLF01", null, null],
        ]);

        var response = await RunImportAsync(client, "/api/users/import-employees", content);
        var body = await response.Content.ReadFromJsonAsync<EmployeeImportResponseDto>();

        Assert.Equal(1, body!.CreatedCount);
        Assert.Equal(1, body.SkippedCount);
        Assert.Equal(2, Assert.Single(body.Warnings).Row);
    }
}
