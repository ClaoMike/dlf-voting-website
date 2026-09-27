using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Infrastructure;

public static class VotingSettingsSeeder
{
    private static readonly Guid SettingsRowId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>Creates the (open) settings row if missing; safe when several app instances start at the same time.</summary>
    public static async Task SeedAsync(DlfVotingDbContext context)
    {
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO "VotingSettings" ("Id", "IsVotingOpen", "UpdatedAt")
            VALUES ({SettingsRowId}, TRUE, {DateTime.UtcNow})
            ON CONFLICT ("Id") DO NOTHING
            """);
    }
}
