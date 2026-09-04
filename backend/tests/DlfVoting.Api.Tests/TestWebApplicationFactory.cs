using DlfVoting.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DlfVoting.Api.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestConnectionString =
        "Host=localhost;Database=dlf_voting_test;Username=" + "claomike";

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