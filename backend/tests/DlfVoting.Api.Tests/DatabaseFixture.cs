using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;

namespace DlfVoting.Api.Tests;

public class DatabaseFixture : IAsyncLifetime
{
    private Respawner _respawner = null!;
    private NpgsqlConnection _connection = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<DlfVotingDbContext>()
            .UseNpgsql(TestWebApplicationFactory.TestConnectionString)
            .Options;

        await using (var context = new DlfVotingDbContext(options))
        {
            await context.Database.MigrateAsync();
        }

        _connection = new NpgsqlConnection(TestWebApplicationFactory.TestConnectionString);
        await _connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = new[] { "public" },
            TablesToIgnore = new[] { new Respawn.Graph.Table("__EFMigrationsHistory") }
        });
    }

    public async Task ResetAsync()
    {
        await _respawner.ResetAsync(_connection);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}