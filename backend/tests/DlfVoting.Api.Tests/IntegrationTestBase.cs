using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DlfVoting.Api.Tests;

public abstract class IntegrationTestBase : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime, IAsyncDisposable
{
    protected TestWebApplicationFactory Factory { get; }
    private readonly DatabaseFixture _dbFixture = new();

    // Admins keep their email as username.
    protected const string AdminEmail = "test-admin@example.com";
    protected const string AdminPassword = "correct-horse-battery";

    protected const string UserUsername = "test-user";
    protected const string UserEmail = "test-user@example.com";
    protected const string UserPassword = "correct-horse-battery-staple-1!";

    // ReSharper disable once ConvertToPrimaryConstructor
    protected IntegrationTestBase(TestWebApplicationFactory factory)
    {
        Factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _dbFixture.InitializeAsync();
        await _dbFixture.ResetAsync();
        await SeedAdminAsync();
        await SeedUserAsync();
    }

    public Task DisposeAsync() => ((IAsyncDisposable)this).DisposeAsync().AsTask();

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await ((IAsyncDisposable)_dbFixture).DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private async Task SeedAdminAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();

        db.Administrators.Add(new Administrator
        {
            Id = Guid.NewGuid(),
            Username = AdminEmail,
            Email = AdminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword),
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private async Task SeedUserAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = UserUsername,
            Email = UserEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(UserPassword),
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    protected async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var loginClient = Factory.CreateClient();
        var response = await loginClient.PostAsJsonAsync("/api/auth/admin/login", new
        {
            username = AdminEmail,
            password = AdminPassword
        });

        var cookie = response.Headers.GetValues("Set-Cookie").First().Split(';')[0];

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }

    protected async Task<HttpClient> CreateAuthenticatedUserClientAsync(
        string? username = null, string? password = null)
    {
        var loginClient = Factory.CreateClient();
        var response = await loginClient.PostAsJsonAsync("/api/auth/user/login", new
        {
            username = username ?? UserUsername,
            password = password ?? UserPassword
        });

        var cookie = response.Headers.GetValues("Set-Cookie").First().Split(';')[0];

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }

    /// <summary>
    /// Runs an Excel import the way the browser does (start it, then poll until it finishes) and returns what the
    /// old single-request endpoint returned: 200 with the import's result, 400 with the message when the import
    /// failed, or the start request's own response when no import started (401, 400 for a bad file, 409 while
    /// another import is running).
    /// </summary>
    protected static async Task<HttpResponseMessage> RunImportAsync(HttpClient client, string path, HttpContent content)
    {
        var start = await client.PostAsync(path, content);
        if (start.StatusCode != HttpStatusCode.Accepted) return start;

        var id = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (true)
        {
            var poll = await client.GetAsync($"/api/users/imports/{id}");
            poll.EnsureSuccessStatusCode();
            var job = await poll.Content.ReadFromJsonAsync<JsonElement>();

            switch (job.GetProperty("status").GetString())
            {
                case "succeeded":
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(job.GetProperty("result")) };
                case "failed":
                    return new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = JsonContent.Create(new { message = job.GetProperty("message").GetString() })
                    };
            }

            if (DateTime.UtcNow > deadline) throw new TimeoutException($"Import {id} did not finish.");
            await Task.Delay(50);
        }
    }
}
