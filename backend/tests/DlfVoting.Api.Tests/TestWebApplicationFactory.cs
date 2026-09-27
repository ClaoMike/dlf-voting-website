using DlfVoting.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DlfVoting.Api.Tests;

// ReSharper disable once ClassNeverInstantiated.Global
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    // The pool stays below Postgres' max_connections (100 by default), so a burst of requests queues for a
    // connection instead of being refused by the server.
    public const string TestConnectionString =
        "Host=localhost;Database=dlf_voting_test;Username=" + "claomike" + ";Maximum Pool Size=40";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<DlfVotingDbContext>>();
            services.AddDbContext<DlfVotingDbContext>(options =>
                options.UseNpgsql(TestConnectionString));
        });
    }
}