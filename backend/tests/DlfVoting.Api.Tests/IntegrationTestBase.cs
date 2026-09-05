using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;

namespace DlfVoting.Api.Tests;

public abstract class IntegrationTestBase : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    protected readonly TestWebApplicationFactory Factory;
    private readonly DatabaseFixture _dbFixture = new();

    protected const string AdminEmail = "test-admin@example.com";
    protected const string AdminPassword = "correct-horse-battery";

    protected IntegrationTestBase(TestWebApplicationFactory factory)
    {
        Factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _dbFixture.InitializeAsync();
        await _dbFixture.ResetAsync();
        await SeedAdminAsync();
    }

    public Task DisposeAsync() => _dbFixture.DisposeAsync();

    private async Task SeedAdminAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();

        db.Administrators.Add(new Administrator
        {
            Id = Guid.NewGuid(),
            Email = AdminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword),
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    protected async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var loginClient = Factory.CreateClient();
        var response = await loginClient.PostAsJsonAsync("/api/auth/admin/login", new
        {
            email = AdminEmail,
            password = AdminPassword
        });

        var cookie = response.Headers.GetValues("Set-Cookie").First().Split(';')[0];

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }
}