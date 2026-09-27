using System.Net;
using System.Net.Http.Json;
using DlfVoting.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable ClassNeverInstantiated.Local
// ReSharper disable NotAccessedPositionalProperty.Local

namespace DlfVoting.Api.Tests.Tests;

/// <summary>
/// What the app needs to run in Azure: the website served next to the API, security headers, the health check,
/// the first administrator from configuration, login rate limiting, and cookie keys kept in the database.
/// </summary>
public class ProductionReadinessTests : IntegrationTestBase
{
    private record MessageDto(string Message);

    private const string InitialAdminEmail = "first-admin@example.com";
    private const string InitialAdminPassword = "FirstAdminPassword1234!@#";

    // ReSharper disable once ConvertToPrimaryConstructor
    public ProductionReadinessTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    private WebApplicationFactory<Program> FactoryWithSettings(params (string Key, string Value)[] settings) =>
        Factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

    private async Task RemoveAllAdministratorsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        await db.Administrators.ExecuteDeleteAsync();
    }

    private async Task<List<string>> AdministratorUsernamesAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        return await db.Administrators.Select(a => a.Username).ToListAsync();
    }

    // --- Security headers ---

    [Theory]
    [InlineData("/api/settings/voting")]
    [InlineData("/api/auth/user/login")]
    [InlineData("/healthz")]
    [InlineData("/welcome")]
    public async Task EveryResponse_CarriesTheSecurityHeaders(string path)
    {
        var response = await Factory.CreateClient().GetAsync(path);

        Assert.Equal(SecurityHeaders.ContentSecurityPolicy, response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("frame-ancestors 'none'", SecurityHeaders.ContentSecurityPolicy);
        Assert.DoesNotContain("'unsafe-inline'", SecurityHeaders.ContentSecurityPolicy.Split(';').Single(d => d.Trim().StartsWith("script-src")));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public async Task HttpsResponses_OutsideDevelopment_SendHsts()
    {
        // Not localhost: ASP.NET never sends HSTS to localhost, so developers' browsers don't get stuck on HTTPS.
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://voting.example.com") });

        var response = await client.GetAsync("/healthz");

        Assert.Contains("max-age=31536000", response.Headers.GetValues("Strict-Transport-Security").Single());
    }

    [Fact]
    public async Task RequestForwardedAsHttps_ByAzure_IsNotRedirectedAgain()
    {
        // Azure passes HTTPS requests on as plain HTTP with X-Forwarded-Proto: https. Without honouring that header,
        // the HTTPS redirect would send the browser round in a loop.
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var request = new HttpRequestMessage(HttpMethod.Get, "/healthz");
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- Health check ---

    [Fact]
    public async Task HealthCheck_WithoutSignIn_ReportsHealthy()
    {
        var response = await Factory.CreateClient().GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    // --- Website served next to the API ---

    [Fact]
    public async Task UnknownApiRoute_IsA404_NotTheWebsite()
    {
        var response = await Factory.CreateClient().GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task WebsitePages_AssetsAndCaching_AreServedFromWwwroot()
    {
        var webRoot = Directory.CreateTempSubdirectory("dlfvoting-wwwroot").FullName;
        Directory.CreateDirectory(Path.Combine(webRoot, "assets"));
        await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), "<!doctype html><title>Koncernvalg</title>");
        await File.WriteAllTextAsync(Path.Combine(webRoot, "assets", "index-abc123.js"), "console.log('app')");

        try
        {
            var client = Factory.WithWebHostBuilder(builder => builder.UseWebRoot(webRoot)).CreateClient();

            // A page of the React app (client-side route) gets index.html, always revalidated.
            var page = await client.GetAsync("/admin/users");
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Contains("<title>Koncernvalg</title>", await page.Content.ReadAsStringAsync());
            Assert.Equal("no-cache", page.Headers.CacheControl?.ToString());

            // Hashed build assets are cached for a year.
            var asset = await client.GetAsync("/assets/index-abc123.js");
            Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
            Assert.Contains("immutable", asset.Headers.CacheControl?.ToString());

            // The API still answers, and unknown API routes don't fall through to the page.
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/nope")).StatusCode);
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    // --- First administrator from configuration ---

    [Fact]
    public async Task InitialAdmin_OnAnEmptyDatabase_IsCreatedFromSettings_AndCanSignIn()
    {
        await RemoveAllAdministratorsAsync();

        var factory = FactoryWithSettings(("InitialAdmin:Email", InitialAdminEmail), ("InitialAdmin:Password", InitialAdminPassword));
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/admin/login",
            new { username = InitialAdminEmail, password = InitialAdminPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([InitialAdminEmail], await AdministratorUsernamesAsync());
    }

    [Fact]
    public async Task InitialAdmin_WithACustomUsername_UsesIt()
    {
        await RemoveAllAdministratorsAsync();

        var factory = FactoryWithSettings(
            ("InitialAdmin:Email", InitialAdminEmail), ("InitialAdmin:Password", InitialAdminPassword), ("InitialAdmin:Username", "first-admin"));
        _ = factory.CreateClient();

        Assert.Equal(["first-admin"], await AdministratorUsernamesAsync());
    }

    [Fact]
    public async Task InitialAdmin_WhenAdministratorsAlreadyExist_IsNotCreated()
    {
        var factory = FactoryWithSettings(("InitialAdmin:Email", InitialAdminEmail), ("InitialAdmin:Password", InitialAdminPassword));
        _ = factory.CreateClient();

        Assert.Equal([AdminEmail], await AdministratorUsernamesAsync());
    }

    [Fact]
    public async Task InitialAdmin_WithoutSettings_CreatesNothing()
    {
        await RemoveAllAdministratorsAsync();

        _ = FactoryWithSettings().CreateClient();

        Assert.Empty(await AdministratorUsernamesAsync());
    }

    [Theory]
    [InlineData("not-an-email", InitialAdminPassword)]
    [InlineData(InitialAdminEmail, "too-weak")]
    public async Task InitialAdmin_WithInvalidSettings_StopsTheAppFromStarting(string email, string password)
    {
        await RemoveAllAdministratorsAsync();

        var factory = FactoryWithSettings(("InitialAdmin:Email", email), ("InitialAdmin:Password", password));

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("InitialAdmin", error.ToString());
        Assert.Empty(await AdministratorUsernamesAsync());
    }

    // --- Startup migrations ---

    [Fact]
    public async Task MigrateOnStartup_OnAnUpToDateDatabase_StartsNormally()
    {
        var client = FactoryWithSettings(("Database:MigrateOnStartup", "true")).CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/healthz")).StatusCode);
    }

    // --- Cookie encryption keys ---

    [Fact]
    public async Task CookieKeys_AreStoredInTheDatabase_SoAnotherInstanceAcceptsTheSession()
    {
        var loginResponse = await Factory.CreateClient().PostAsJsonAsync("/api/auth/user/login",
            new { username = UserUsername, password = UserPassword });
        var cookie = loginResponse.Headers.GetValues("Set-Cookie").First().Split(';')[0];

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
            Assert.True(await db.DataProtectionKeys.AnyAsync());
        }

        // A second app instance (a restart, or scale-out) reads the keys from the database.
        var otherInstance = FactoryWithSettings().CreateClient();
        otherInstance.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.OK, (await otherInstance.GetAsync("/api/auth/user/me")).StatusCode);
    }
}
