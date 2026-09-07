using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Infrastructure;

public static class VotingSettingsSeeder
{
    private static readonly Guid SettingsRowId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(DlfVotingDbContext context)
    {
        var exists = await context.VotingSettings.AnyAsync();
        if (exists) return;

        context.VotingSettings.Add(new Domain.VotingSettings
        {
            Id = SettingsRowId,
            IsVotingOpen = true,
            UpdatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }
}