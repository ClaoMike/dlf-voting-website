using System.Net;
using System.Net.Http.Json;

namespace DlfVoting.Api.Tests;

public class VotingOptionsControllerTests : IntegrationTestBase
{
    private record VotingOptionResponseDto(Guid Id, string Name, DateTime CreatedAt);

    public VotingOptionsControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    private static async Task<Guid> CreateOptionAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/voting-options", new { name });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<VotingOptionResponseDto>();
        return body!.Id;
    }

    // --- Auth ---

    [Fact]
    public async Task GetAll_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/voting-options");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutAuth_ReturnsUnauthorized()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/voting-options", new { name = "Test" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithEmptyName_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/voting-options", new { name = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithValidName_AppearsInList()
    {
        var client = await CreateAuthenticatedClientAsync();
        var createResponse = await client.PostAsJsonAsync("/api/voting-options", new { name = "Board Member" });
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/voting-options");
        var options = await listResponse.Content.ReadFromJsonAsync<List<VotingOptionResponseDto>>();
        Assert.Contains(options!, o => o.Name == "Board Member");
    }

    [Fact]
    public async Task Create_WithDuplicateName_ReturnsConflict()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateOptionAsync(client, "Duplicate Name");

        var response = await client.PostAsJsonAsync("/api/voting-options", new { name = "Duplicate Name" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // --- Read / ordering ---

    [Fact]
    public async Task GetAll_ReturnsOptionsSortedAlphabetically()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateOptionAsync(client, "Zebra");
        await CreateOptionAsync(client, "Apple");
        await CreateOptionAsync(client, "Mango");

        var response = await client.GetAsync("/api/voting-options");
        var options = await response.Content.ReadFromJsonAsync<List<VotingOptionResponseDto>>();

        var names = options!.Select(o => o.Name).ToList();
        var expected = names.OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.Equal(expected, names);
    }

    // --- Update ---

    [Fact]
    public async Task Update_WithValidName_UpdatesOption()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateOptionAsync(client, "Old Name");

        var response = await client.PutAsJsonAsync($"/api/voting-options/{id}", new { name = "New Name" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<VotingOptionResponseDto>();
        Assert.Equal("New Name", body!.Name);
    }

    [Fact]
    public async Task Update_WithEmptyName_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateOptionAsync(client, "Some Name");

        var response = await client.PutAsJsonAsync($"/api/voting-options/{id}", new { name = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_NonexistentId_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PutAsJsonAsync($"/api/voting-options/{Guid.NewGuid()}", new { name = "Whatever" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ToNameTakenByAnotherOption_ReturnsConflict()
    {
        var client = await CreateAuthenticatedClientAsync();
        await CreateOptionAsync(client, "Taken Name");
        var id2 = await CreateOptionAsync(client, "Other Name");

        var response = await client.PutAsJsonAsync($"/api/voting-options/{id2}", new { name = "Taken Name" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_ExistingOption_RemovesItFromList()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateOptionAsync(client, "To Delete");

        var deleteResponse = await client.DeleteAsync($"/api/voting-options/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/voting-options");
        var options = await listResponse.Content.ReadFromJsonAsync<List<VotingOptionResponseDto>>();
        Assert.DoesNotContain(options!, o => o.Id == id);
    }

    [Fact]
    public async Task Delete_NonexistentId_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.DeleteAsync($"/api/voting-options/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AlreadyDeletedOption_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateOptionAsync(client, "Delete Twice");
        await client.DeleteAsync($"/api/voting-options/{id}");

        var secondResponse = await client.DeleteAsync($"/api/voting-options/{id}");
        Assert.Equal(HttpStatusCode.NotFound, secondResponse.StatusCode);
    }

    // --- Concurrency / race conditions ---

    [Fact]
    public async Task ConcurrentCreate_SameName_ExactlyOneSucceeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        const string name = "Concurrent Create Target";

        var task1 = client.PostAsJsonAsync("/api/voting-options", new { name });
        var task2 = client.PostAsJsonAsync("/api/voting-options", new { name });
        var responses = await Task.WhenAll(task1, task2);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var listResponse = await client.GetAsync("/api/voting-options");
        var options = await listResponse.Content.ReadFromJsonAsync<List<VotingOptionResponseDto>>();
        Assert.Single(options!, o => o.Name == name);
    }

    [Fact]
    public async Task ConcurrentUpdate_DifferentOptionsToSameName_ExactlyOneSucceeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id1 = await CreateOptionAsync(client, "Option One");
        var id2 = await CreateOptionAsync(client, "Option Two");
        const string targetName = "Contested Name";

        var task1 = client.PutAsJsonAsync($"/api/voting-options/{id1}", new { name = targetName });
        var task2 = client.PutAsJsonAsync($"/api/voting-options/{id2}", new { name = targetName });
        var responses = await Task.WhenAll(task1, task2);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task ConcurrentDelete_SameOption_ExactlyOneSucceeds()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateOptionAsync(client, "Double Delete Target");

        var task1 = client.DeleteAsync($"/api/voting-options/{id}");
        var task2 = client.DeleteAsync($"/api/voting-options/{id}");
        var responses = await Task.WhenAll(task1, task2);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.NoContent));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task ConcurrentUpdateAndDelete_SameOption_NeverBothSucceedWithStaleData()
    {
        var client = await CreateAuthenticatedClientAsync();
        var id = await CreateOptionAsync(client, "Contested Row");

        var updateTask = client.PutAsJsonAsync($"/api/voting-options/{id}", new { name = "Updated Name" });
        var deleteTask = client.DeleteAsync($"/api/voting-options/{id}");
        var updateResponse = await updateTask;
        var deleteResponse = await deleteTask;

        // The single delete request always succeeds (nothing else contends for it here) -
        // the only question is whether the update landed before or after it.
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.True(
            updateResponse.StatusCode == HttpStatusCode.OK ||
            updateResponse.StatusCode == HttpStatusCode.NotFound,
            $"Unexpected update status: {updateResponse.StatusCode}");

        // Whichever way the race went, the row must no longer exist afterward.
        var listResponse = await client.GetAsync("/api/voting-options");
        var options = await listResponse.Content.ReadFromJsonAsync<List<VotingOptionResponseDto>>();
        Assert.DoesNotContain(options!, o => o.Id == id);
    }
}