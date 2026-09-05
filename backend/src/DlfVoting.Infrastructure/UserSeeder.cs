using DlfVoting.Domain;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Infrastructure;

public static class UserSeeder
{
    public static async Task SeedDevUsersAsync(DlfVotingDbContext context)
    {
        var existingCount = await context.Users.CountAsync();
        if (existingCount > 0)
        {
            return;
        }

        var users = new List<User>();
        for (var i = 1; i <= 70; i++)
        {
            users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = $"user{i:D2}@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("DevSeedPassword1234!@#"),
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }

        context.Users.AddRange(users);
        await context.SaveChangesAsync();
    }
}