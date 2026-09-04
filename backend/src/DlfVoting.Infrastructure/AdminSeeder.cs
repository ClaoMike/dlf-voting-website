using DlfVoting.Domain;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Infrastructure;

public static class AdminSeeder
{
    public static async Task SeedDefaultAdminAsync(DlfVotingDbContext context)
    {
        const string defaultEmail = "jechelclaudiumihai@gmail.com";

        var exists = await context.Administrators
            .AnyAsync(a => a.Email == defaultEmail);

        if (exists)
        {
            return;
        }

        var admin = new Administrator
        {
            Id = Guid.NewGuid(),
            Email = defaultEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("gacfYp-zyvjaj-fovde9"),
            CreatedAt = DateTime.UtcNow
        };

        context.Administrators.Add(admin);
        await context.SaveChangesAsync();
    }
}