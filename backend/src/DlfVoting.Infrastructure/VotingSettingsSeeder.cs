using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Infrastructure;

public static class VotingSettingsSeeder
{
    public static async Task SeedAsync(DlfVotingDbContext context)
    {
        var exists = await context.VotingSettings.AnyAsync();
        if (exists) return;

        context.VotingSettings.Add(new Domain.VotingSettings
        {
            Id = Guid.NewGuid(),
            IsVotingOpen = true,
            UpdatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }
}