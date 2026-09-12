using System.Net;
using System.Net.Http.Json;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DlfVoting.Api.Tests.Tests;

public class AdministratorsControllerTests : IntegrationTestBase
{
    private record AdministratorResponseDto(Guid Id, string Email, DateTime CreatedAt);
    private record PagedAdministratorsResponseDto(List<AdministratorResponseDto> Items, int TotalCount, int Page, int PageSize);

    private const string ValidPassword = "ValidPassword1234!@#$";
    
    // ReSharper disable once ConvertToPrimaryConstructor
    public AdministratorsControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    private static async Task<Guid> CreateAdminAsync(HttpClient client, string email, string password = ValidPassword)
    {
        var response = await client.PostAsJsonAsync("/api/administrators", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AdministratorResponseDto>();
        return body!.Id;
    }

    private async Task<HttpClient> CreateSecondAdminAndLoginAsync(string email, string password)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        db.Administrators.Add(new Administrator
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var loginClient = Factory.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/admin/login", new { email, password });
        var cookie = loginResponse.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }

    // --- Auth ---

    [Fact]
    public async Task GetPage_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/administrators");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPage_WithUserAuth_ReturnsUnauthorized()
    {
        var userClient = await CreateAuthenticatedUserClientAsync();
        var response = await userClient.GetAsync("/api/administrators");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/administrators", new { email = "a@b.com", password = ValidPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.PutAsJsonAsync($"/api/administrators/{Guid.NewGuid()}", new { email = "a@b.com" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.DeleteAsync($"/api/administrators/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithInvalidEmail_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/administrators", new { email = "not-an-email", password = ValidPassword });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Short1!")]
    [InlineData("nouppercasehere1234567!@#")]
    [InlineData("NoDigitsHereAtAllForSure!@#")]
    [InlineData("NoSpecialCharacters12345678")]
    public async Task Create_WithInvalidPassword_ReturnsBadRequest(string password)
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/administrators", new { email = "valid@example.com", password });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithValidData_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/administrators", new { email = "newadmin@example.com", password = ValidPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AdministratorResponseDto>();
        Assert.Equal("newadmin@example.com", body!.Email);
    }

    [Fact]
    public async Task Create_WithDuplicateEmail_ReturnsConflict()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateAdminAsync(client, "dupadmin@example.com");

        var response = await client.PostAsJsonAsync("/api/administrators", new { email = "dupadmin@example.com", password = ValidPassword });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // --- Pagination / ordering ---

    [Fact]
    public async Task GetPage_CurrentAdmin_AlwaysAppearsFirst()
    {
        var client = await CreateAuthenticatedClientAsync();
        // Names chosen to alphabetically precede the seeded admin's email, to prove
        // ordering isn't simply alphabetical.
        await CreateAdminAsync(client, "aaa-should-be-second@example.com");
        await CreateAdminAsync(client, "aab-should-be-third@example.com");

        var response = await client.GetAsync("/api/administrators?page=1");
        var body = await response.Content.ReadFromJsonAsync<PagedAdministratorsResponseDto>();

        Assert.Equal(AdminEmail, body!.Items[0].Email);
    }

    [Fact]
    public async Task GetPage_NonCurrentAdmins_AreSortedAlphabeticallyAfterCurrent()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateAdminAsync(client, "zzz-third@example.com");
        await CreateAdminAsync(client, "aaa-second@example.com");

        var response = await client.GetAsync("/api/administrators?page=1");
        var body = await response.Content.ReadFromJsonAsync<PagedAdministratorsResponseDto>();

        var rest = body!.Items.Skip(1).Select(a => a.Email).ToList();
        Assert.Equal(new[] { "aaa-second@example.com", "zzz-third@example.com" }, rest);
    }

    [Fact]
    public async Task GetPage_ReturnsUpTo25ItemsSortedWithCurrentAdminFirst()
    {
        var client = await CreateAuthenticatedClientAsync();
        for (var i = 0; i < 30; i++)
        {
            await CreateAdminAsync(client, $"bulk-admin-{i:D2}@example.com");
        }

        var response = await client.GetAsync("/api/administrators?page=1");
        var body = await response.Content.ReadFromJsonAsync<PagedAdministratorsResponseDto>();

        // +1 for the base seeded admin.
        Assert.Equal(31, body!.TotalCount);
        Assert.Equal(25, body.Items.Count);
        Assert.Equal(AdminEmail, body.Items[0].Email);
    }

    [Fact]
    public async Task GetPage_SecondPage_ReturnsRemainingItems()
    {
        var client = await CreateAuthenticatedClientAsync();
        for (var i = 0; i < 30; i++)
        {
            await CreateAdminAsync(client, $"bulk2-admin-{i:D2}@example.com");
        }

        var response = await client.GetAsync("/api/administrators?page=2");
        var body = await response.Content.ReadFromJsonAsync<PagedAdministratorsResponseDto>();

        Assert.Equal(2, body!.Page);
        Assert.Equal(6, body.Items.Count);
    }

    // --- Self-protection ---

    [Fact]
    public async Task Update_OnOwnAccount_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedClientAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var self = await db.Administrators.FirstAsync(a => a.Email == AdminEmail);

        var response = await client.PutAsJsonAsync($"/api/administrators/{self.Id}", new { email = "trying-to-change-self@example.com" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_OnOwnAccount_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedClientAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var self = await db.Administrators.FirstAsync(a => a.Email == AdminEmail);

        var response = await client.DeleteAsync($"/api/administrators/{self.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Confirm the account genuinely still exists afterward.
        var stillExists = await db.Administrators.AnyAsync(a => a.Id == self.Id);
        Assert.True(stillExists);
    }

    [Fact]
    public async Task Update_OnAnotherAdmin_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var otherId = await CreateAdminAsync(client, "editable-other@example.com");

        var response = await client.PutAsJsonAsync($"/api/administrators/{otherId}", new { email = "edited-other@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Delete_OnAnotherAdmin_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var otherId = await CreateAdminAsync(client, "deletable-other@example.com");

        var response = await client.DeleteAsync($"/api/administrators/{otherId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Update_OnOwnAccount_ByDifferentLoggedInAdmin_Succeeds()
    {
        // Admin B editing Admin A's account (not their own) must work fine —
        // the self-protection is per-session, not a blanket rule on any specific account.
        var adminAClient = await CreateAuthenticatedClientAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var adminAId = (await db.Administrators.FirstAsync(a => a.Email == AdminEmail)).Id;

        var adminBClient = await CreateSecondAdminAndLoginAsync("admin-b@example.com", ValidPassword);

        var response = await adminBClient.PutAsJsonAsync($"/api/administrators/{adminAId}", new { email = "admin-a-renamed@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- Not found / conflict ---

    [Fact]
    public async Task Update_NonexistentId_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PutAsJsonAsync($"/api/administrators/{Guid.NewGuid()}", new { email = "whoever@example.com" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NonexistentId_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.DeleteAsync($"/api/administrators/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AlreadyDeleted_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync();
        var otherId = await CreateAdminAsync(client, "delete-twice@example.com");
        await client.DeleteAsync($"/api/administrators/{otherId}");

        var second = await client.DeleteAsync($"/api/administrators/{otherId}");
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    [Fact]
    public async Task Update_ToEmailTakenByAnotherAdmin_ReturnsConflict()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateAdminAsync(client, "taken-admin@example.com");
        var otherId = await CreateAdminAsync(client, "other-admin@example.com");

        var response = await client.PutAsJsonAsync($"/api/administrators/{otherId}", new { email = "taken-admin@example.com" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithNeitherFieldProvided_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var otherId = await CreateAdminAsync(client, "neither-field@example.com");

        var response = await client.PutAsJsonAsync($"/api/administrators/{otherId}", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Concurrency ---

    [Fact]
    public async Task ConcurrentCreate_SameEmail_ExactlyOneSucceeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        const string email = "race-create-admin@example.com";

        var task1 = client.PostAsJsonAsync("/api/administrators", new { email, password = ValidPassword });
        var task2 = client.PostAsJsonAsync("/api/administrators", new { email, password = ValidPassword });
        var responses = await Task.WhenAll(task1, task2);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task ConcurrentUpdate_DifferentAdminsToSameEmail_ExactlyOneSucceeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id1 = await CreateAdminAsync(client, "race-target-1@example.com");
        var id2 = await CreateAdminAsync(client, "race-target-2@example.com");
        const string targetEmail = "race-contested-admin@example.com";

        var task1 = client.PutAsJsonAsync($"/api/administrators/{id1}", new { email = targetEmail });
        var task2 = client.PutAsJsonAsync($"/api/administrators/{id2}", new { email = targetEmail });
        var responses = await Task.WhenAll(task1, task2);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task ConcurrentDelete_SameAdmin_ExactlyOneSucceeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var otherId = await CreateAdminAsync(client, "race-delete-admin@example.com");

        var task1 = client.DeleteAsync($"/api/administrators/{otherId}");
        var task2 = client.DeleteAsync($"/api/administrators/{otherId}");
        var responses = await Task.WhenAll(task1, task2);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.NoContent));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task ConcurrentUpdateAndDelete_SameAdmin_NeverLeavesInconsistentState()
    {
        var client = await CreateAuthenticatedClientAsync();
        var otherId = await CreateAdminAsync(client, "race-update-delete-admin@example.com");

        var updateTask = client.PutAsJsonAsync($"/api/administrators/{otherId}", new { email = "updated-race-admin@example.com" });
        var deleteTask = client.DeleteAsync($"/api/administrators/{otherId}");
        var updateResponse = await updateTask;
        var deleteResponse = await deleteTask;

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.True(
            updateResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var stillExists = await db.Administrators.AnyAsync(a => a.Id == otherId);
        Assert.False(stillExists);
    }

    [Fact]
    public async Task ConcurrentTwoAdminsEditingEachOthersDifferentTargets_BothSucceedIndependently()
    {
        // Admin A (session 1) edits target X, while a second logged-in admin (session 2)
        // simultaneously edits a completely different target Y — no contention, both succeed.
        var adminAClient = await CreateAuthenticatedClientAsync();
        var targetX = await CreateAdminAsync(adminAClient, "independent-target-x@example.com");
        var targetY = await CreateAdminAsync(adminAClient, "independent-target-y@example.com");

        var adminBClient = await CreateSecondAdminAndLoginAsync("admin-b-independent@example.com", ValidPassword);

        var task1 = adminAClient.PutAsJsonAsync($"/api/administrators/{targetX}", new { email = "renamed-x@example.com" });
        var task2 = adminBClient.PutAsJsonAsync($"/api/administrators/{targetY}", new { email = "renamed-y@example.com" });
        var responses = await Task.WhenAll(task1, task2);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
    }

    [Fact]
    public async Task ConcurrentDeleteAttemptsOnSelf_FromTwoAdmins_OneIsBlockedOneSucceeds()
    {
        // Admin A tries to delete Admin B while, at the same instant, Admin B tries to
        // delete themselves (which must always be blocked regardless of timing).
        var adminAClient = await CreateAuthenticatedClientAsync();
        var adminBEmail = "self-delete-race-target@example.com";
        var adminBClient = await CreateSecondAdminAndLoginAsync(adminBEmail, ValidPassword);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var adminBId = (await db.Administrators.FirstAsync(a => a.Email == adminBEmail)).Id;

        var taskFromA = adminAClient.DeleteAsync($"/api/administrators/{adminBId}");
        var taskFromBOnSelf = adminBClient.DeleteAsync($"/api/administrators/{adminBId}");
        var responses = await Task.WhenAll(taskFromA, taskFromBOnSelf);

        // B's self-delete attempt must always be 403, regardless of what A's request does.
        var bResponse = await taskFromBOnSelf;
        Assert.Equal(HttpStatusCode.Forbidden, bResponse.StatusCode);

        // A's request (not a self-delete) must succeed cleanly.
        var aResponse = await taskFromA;
        Assert.Equal(HttpStatusCode.NoContent, aResponse.StatusCode);
    }
    
    // --- ChangeOwnPassword ---
    [Fact]
    public async Task ChangeOwnPassword_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.PutAsJsonAsync("/api/administrators/me/password", new { password = ValidPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangeOwnPassword_WithUserAuth_ReturnsUnauthorized()
    {
        var userClient = await CreateAuthenticatedUserClientAsync();
        var response = await userClient.PutAsJsonAsync("/api/administrators/me/password", new { password = ValidPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Short1!")]
    [InlineData("nouppercasehere1234567!@#")]
    [InlineData("NoDigitsHereAtAllForSure!@#")]
    [InlineData("NoSpecialCharacters12345678")]
    public async Task ChangeOwnPassword_WithInvalidPassword_ReturnsBadRequest(string password)
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PutAsJsonAsync("/api/administrators/me/password", new { password });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeOwnPassword_WithValidPassword_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        const string newPassword = "BrandNewValidPassword1!@#";

        var response = await client.PutAsJsonAsync("/api/administrators/me/password", new { password = newPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AdministratorResponseDto>();
        Assert.Equal(AdminEmail, body!.Email);
    }

    [Fact]
    public async Task ChangeOwnPassword_ThenLoginWithNewPassword_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        const string newPassword = "AnotherBrandNewPassword1!@#";
        await client.PutAsJsonAsync("/api/administrators/me/password", new { password = newPassword });

        var loginClient = Factory.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/admin/login", new
        {
            email = AdminEmail,
            password = newPassword
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task ChangeOwnPassword_ThenLoginWithOldPassword_Fails()
    {
        var client = await CreateAuthenticatedClientAsync();
        await client.PutAsJsonAsync("/api/administrators/me/password", new { password = "YetAnotherNewPassword1!@#" });

        var loginClient = Factory.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/admin/login", new
        {
            email = AdminEmail,
            password = AdminPassword // the original password from IntegrationTestBase seeding
        });

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task ChangeOwnPassword_DoesNotAffectOtherAdminsSessions()
    {
        var selfClient = await CreateAuthenticatedClientAsync();
        var otherAdminClient = await CreateSecondAdminAndLoginAsync("unaffected-admin@example.com", ValidPassword);

        await selfClient.PutAsJsonAsync("/api/administrators/me/password", new { password = "SelfOnlyChange123!@#" });

        // The other admin's own session and endpoint access must remain completely unaffected.
        var response = await otherAdminClient.GetAsync("/api/administrators");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- ChangeOwnPassword: concurrency ---

    [Fact]
    public async Task ConcurrentChangeOwnPassword_TwoSimultaneousRequests_BothSucceedAndFinalPasswordWorks()
    {
        var client = await CreateAuthenticatedClientAsync();
        const string passwordA = "ConcurrentPasswordA1!@#";
        const string passwordB = "ConcurrentPasswordB1!@#";

        var task1 = client.PutAsJsonAsync("/api/administrators/me/password", new { password = passwordA });
        var task2 = client.PutAsJsonAsync("/api/administrators/me/password", new { password = passwordB });
        var responses = await Task.WhenAll(task1, task2);

        // A simple field update with no uniqueness constraint involved — both requests
        // should succeed cleanly (last write wins), never crash or conflict.
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        // Exactly one of the two passwords must now be the valid one — confirm by trying both.
        var loginClient = Factory.CreateClient();
        var loginA = await loginClient.PostAsJsonAsync("/api/auth/admin/login", new { email = AdminEmail, password = passwordA });
        var loginB = await loginClient.PostAsJsonAsync("/api/auth/admin/login", new { email = AdminEmail, password = passwordB });

        var successes = new[] { loginA.StatusCode, loginB.StatusCode }.Count(s => s == HttpStatusCode.OK);
        Assert.Equal(1, successes);
    }

    [Fact]
    public async Task ConcurrentChangeOwnPasswordFromTwoDifferentAdmins_EachOnlyAffectsThemselves()
    {
        var adminAClient = await CreateAuthenticatedClientAsync();
        const string adminBEmail = "concurrent-self-change-b@example.com";
        const string adminBOriginalPassword = ValidPassword;
        var adminBClient = await CreateSecondAdminAndLoginAsync(adminBEmail, adminBOriginalPassword);

        const string adminANewPassword = "AdminAConcurrentNew1!@#";
        const string adminBNewPassword = "AdminBConcurrentNew1!@#";

        var taskA = adminAClient.PutAsJsonAsync("/api/administrators/me/password", new { password = adminANewPassword });
        var taskB = adminBClient.PutAsJsonAsync("/api/administrators/me/password", new { password = adminBNewPassword });
        var responses = await Task.WhenAll(taskA, taskB);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        var loginClient = Factory.CreateClient();
        var loginAWithNew = await loginClient.PostAsJsonAsync("/api/auth/admin/login", new { email = AdminEmail, password = adminANewPassword });
        var loginBWithNew = await loginClient.PostAsJsonAsync("/api/auth/admin/login", new { email = adminBEmail, password = adminBNewPassword });

        Assert.Equal(HttpStatusCode.OK, loginAWithNew.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginBWithNew.StatusCode);
    }

}