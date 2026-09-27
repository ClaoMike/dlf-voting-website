using DlfVoting.Api.Common;
using DlfVoting.Api.Validation;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DlfVoting.Api;

/// <summary>Runs once when the app starts, before it serves requests.</summary>
public static class StartupTasks
{
    public static async Task RunAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(StartupTasks));

        // Off by default (tests and local development migrate separately); switch on in Azure with Database__MigrateOnStartup.
        if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            logger.LogInformation("Applying database migrations");
            await db.Database.MigrateAsync();
        }

        await VotingSettingsSeeder.SeedAsync(db);
        await CreateInitialAdministratorAsync(db, app.Configuration, logger);

        if (app.Environment.IsDevelopment())
        {
            await UserSeeder.SeedDevUsersAsync(db);
        }
    }

    /// <summary>
    /// A fresh database has no administrator, so nobody could sign in. When the InitialAdmin settings are present and
    /// there are no administrators yet, this creates one. Credentials come from configuration (Azure app settings,
    /// or user-secrets locally), never from source code; remove the settings once you have signed in.
    /// </summary>
    private static async Task CreateInitialAdministratorAsync(DlfVotingDbContext db, IConfiguration configuration, ILogger logger)
    {
        var email = configuration["InitialAdmin:Email"]?.Trim();
        var password = configuration["InitialAdmin:Password"];
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)) return;
        if (await db.Administrators.AnyAsync()) return;

        var username = configuration["InitialAdmin:Username"]?.Trim() is { Length: > 0 } configured ? configured : email;

        // Fail loudly at startup rather than create an administrator that the normal rules would reject.
        if (!IdentityRules.IsValidEmail(email))
            throw new InvalidOperationException($"InitialAdmin:Email: {IdentityRules.InvalidEmailMessage}");
        if (!IdentityRules.IsValidUsername(username, Administrator.UsernameMaxLength))
            throw new InvalidOperationException($"InitialAdmin:Username: {IdentityRules.InvalidUsernameMessage(Administrator.UsernameMaxLength)}");
        if (!IdentityRules.IsValidPassword(password))
            throw new InvalidOperationException($"InitialAdmin:Password: {IdentityRules.InvalidPasswordMessage}");

        db.Administrators.Add(new Administrator
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = PasswordHashing.Hash(password),
            CreatedAt = DateTime.UtcNow
        });

        try
        {
            await db.SaveChangesAsync();
            logger.LogWarning("Created the initial administrator {Username}. Remove the InitialAdmin settings now.", username);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            // Another instance starting at the same moment created it first.
        }
    }
}
