using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;

namespace DlfVoting.Api.Tests;

public class DatabaseFixture : IAsyncLifetime, IAsyncDisposable
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
            SchemasToInclude = ["public"],
            TablesToIgnore = [new Respawn.Graph.Table("__EFMigrationsHistory")]
        });
    }

    public async Task ResetAsync()
    {
        await _respawner.ResetAsync(_connection);
    }

    public Task DisposeAsync()
    {
        return ((IAsyncDisposable)this).DisposeAsync().AsTask();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await _connection.DisposeAsync();
        GC.SuppressFinalize(this);
    }
    
}