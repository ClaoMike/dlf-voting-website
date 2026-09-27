using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using DlfVoting.Domain;
using DlfVoting.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable ClassNeverInstantiated.Local
// ReSharper disable NotAccessedPositionalProperty.Local

namespace DlfVoting.Api.Tests.Tests;

/// <summary>
/// Excel imports run in the background: the upload returns straight away with a job id, and the browser polls for
/// progress and the result. On Azure a single request is cut off after 230 s, and a big import used to take longer,
/// losing the file with the generated passwords while the users were still created.
/// </summary>
public class ImportJobsTests : IntegrationTestBase
{
    private record CreatedUserDto(string Email, string Username, string Password);
    private record EmailImportResultDto(List<CreatedUserDto> Created, List<object> Skipped, string? File);
    private record JobDto(Guid Id, string Status, int Processed, int Total, EmailImportResultDto? Result, string? Message);
    private record MessageDto(string Message);

    private const string OtherAdminEmail = "other-admin@example.com";
    private const string OtherAdminPassword = "OtherAdminPassword1234!@#";

    // ReSharper disable once ConvertToPrimaryConstructor
    public ImportJobsTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    private sealed class AdjustableTimeProvider : TimeProvider
    {
        public TimeSpan Offset { get; set; }
        public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + Offset;
    }

    private static MultipartFormDataContent EmailWorkbook(int count, string prefix = "person")
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell(1, 1).SetValue("email");
        for (var i = 0; i < count; i++)
        {
            sheet.Cell(i + 2, 1).SetValue($"{prefix}{i}@example.com");
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var file = new ByteArrayContent(stream.ToArray());
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        return new MultipartFormDataContent { { file, "file", "emails.xlsx" } };
    }

    private static async Task<JobDto> StartAsync(HttpClient client, MultipartFormDataContent upload, string path = "/api/users/bulk-import")
    {
        var response = await client.PostAsync(path, upload);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JobDto>())!;
    }

    private static async Task<JobDto> WaitUntilFinishedAsync(HttpClient client, Guid id)
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (true)
        {
            var job = await client.GetFromJsonAsync<JobDto>($"/api/users/imports/{id}");
            if (job!.Status != "running") return job;
            if (DateTime.UtcNow > deadline) throw new TimeoutException();
            await Task.Delay(50);
        }
    }

    private async Task<HttpClient> CreateSecondAdminClientAsync()
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
            db.Administrators.Add(new Administrator
            {
                Id = Guid.NewGuid(),
                Username = OtherAdminEmail,
                Email = OtherAdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(OtherAdminPassword),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var login = await Factory.CreateClient().PostAsJsonAsync("/api/auth/admin/login", new { username = OtherAdminEmail, password = OtherAdminPassword });
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", login.Headers.GetValues("Set-Cookie").First().Split(';')[0]);
        return client;
    }

    private async Task<int> CountUsersAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
        return await db.Users.CountAsync();
    }

    [Fact]
    public async Task Upload_ReturnsAcceptedWithTheJob_BeforeThePasswordsAreHashed()
    {
        var admin = await CreateAuthenticatedClientAsync();

        var response = await admin.PostAsync("/api/users/bulk-import", EmailWorkbook(100));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var job = (await response.Content.ReadFromJsonAsync<JobDto>())!;
        Assert.Equal($"/api/users/imports/{job.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal("running", job.Status);
        Assert.True(job.Processed < 100, "The upload should answer before all 100 passwords are hashed.");
        Assert.Null(job.Result);

        await WaitUntilFinishedAsync(admin, job.Id);
    }

    [Fact]
    public async Task Polling_ReportsProgress_ThenTheResult_WhosePasswordsWork()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var started = await StartAsync(admin, EmailWorkbook(3));

        var job = await WaitUntilFinishedAsync(admin, started.Id);

        Assert.Equal("succeeded", job.Status);
        Assert.Equal(3, job.Total);
        Assert.Equal(3, job.Processed);
        Assert.Equal(3, job.Result!.Created.Count);
        Assert.NotNull(job.Result.File);
        Assert.Null(job.Message);

        var someone = job.Result.Created[0];
        var login = await Factory.CreateClient().PostAsJsonAsync("/api/auth/user/login", new { username = someone.Username, password = someone.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task ASecondImport_WhileOneIsRunning_IsTurnedAway_AndTheFirstFinishes()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var first = await StartAsync(admin, EmailWorkbook(60, "first"));

        var second = await admin.PostAsync("/api/users/bulk-import", EmailWorkbook(2, "second"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("An import is already running. Please wait for it to finish.", (await second.Content.ReadFromJsonAsync<MessageDto>())!.Message);

        Assert.Equal("succeeded", (await WaitUntilFinishedAsync(admin, first.Id)).Status);
        Assert.Equal(HttpStatusCode.Accepted, (await admin.PostAsync("/api/users/bulk-import", EmailWorkbook(2, "second"))).StatusCode);
    }

    [Fact]
    public async Task AFailedImport_ReportsWhy_AndCreatesNobody()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var usersBefore = await CountUsersAsync();

        // An employee import needs the HR column headers; a plain email list has none.
        var started = await StartAsync(admin, EmailWorkbook(3), "/api/users/import-employees");
        var job = await WaitUntilFinishedAsync(admin, started.Id);

        Assert.Equal("failed", job.Status);
        Assert.Contains("Could not find a header row", job.Message);
        Assert.Null(job.Result);
        Assert.Equal(usersBefore, await CountUsersAsync());
    }

    [Fact]
    public async Task TheResult_IsOnlyVisibleToTheAdministratorWhoStartedIt()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var otherAdmin = await CreateSecondAdminClientAsync();
        var started = await StartAsync(admin, EmailWorkbook(2));
        await WaitUntilFinishedAsync(admin, started.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await otherAdmin.GetAsync($"/api/users/imports/{started.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherAdmin.DeleteAsync($"/api/users/imports/{started.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/users/imports/{started.Id}")).StatusCode);
    }

    [Fact]
    public async Task Deleting_AFinishedImport_ForgetsItsPasswords()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var started = await StartAsync(admin, EmailWorkbook(2));
        await WaitUntilFinishedAsync(admin, started.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/users/imports/{started.Id}")).StatusCode);

        var gone = await admin.GetAsync($"/api/users/imports/{started.Id}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Equal("This import is no longer available.", (await gone.Content.ReadFromJsonAsync<MessageDto>())!.Message);
    }

    [Fact]
    public async Task ARunningImport_CannotBeDeleted()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var started = await StartAsync(admin, EmailWorkbook(60));

        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/users/imports/{started.Id}")).StatusCode);

        Assert.Equal("succeeded", (await WaitUntilFinishedAsync(admin, started.Id)).Status);
    }

    [Fact]
    public async Task AnUnknownImport_IsNotFound()
    {
        var admin = await CreateAuthenticatedClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/users/imports/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Results_AreDiscardedAnHourAfterTheImportFinished()
    {
        var clock = new AdjustableTimeProvider();
        var factory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));

        async Task<HttpClient> SignInAsync()
        {
            var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/admin/login", new { username = AdminEmail, password = AdminPassword });
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Add("Cookie", login.Headers.GetValues("Set-Cookie").First().Split(';')[0]);
            return client;
        }

        var admin = await SignInAsync();
        var started = await StartAsync(admin, EmailWorkbook(2));
        await WaitUntilFinishedAsync(admin, started.Id);

        clock.Offset = TimeSpan.FromMinutes(59);
        admin = await SignInAsync(); // the old session expired along the way
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/users/imports/{started.Id}")).StatusCode);

        clock.Offset = TimeSpan.FromMinutes(61);
        admin = await SignInAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/users/imports/{started.Id}")).StatusCode);
    }
}
