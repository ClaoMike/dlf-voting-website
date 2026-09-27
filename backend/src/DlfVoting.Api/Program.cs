using DlfVoting.Api;
using DlfVoting.Api.Imports;
using DlfVoting.Api.Services;
using DlfVoting.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<DlfVotingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<UserQueryService>();
builder.Services.AddScoped<UserAccountService>();
builder.Services.AddScoped<AdministratorService>();
builder.Services.AddScoped<VoteService>();
builder.Services.AddScoped<VoteReportService>();
builder.Services.AddScoped<EmailImportService>();
builder.Services.AddScoped<EmployeeImportService>();
builder.Services.AddSingleton<LoginAttemptLimiter>();

// The keys that encrypt the session cookies live in the database: sessions survive restarts and redeploys, and
// all instances of the app accept each other's cookies.
builder.Services.AddDataProtection()
    .SetApplicationName("DlfVoting")
    .PersistKeysToDbContext<DlfVotingDbContext>();

// GET /healthz, for Azure App Service's health check: healthy when the app is up and the database answers.
builder.Services.AddHealthChecks().AddDbContextCheck<DlfVotingDbContext>();

// Azure ends HTTPS in front of the app and passes the original scheme on in X-Forwarded-Proto; trust it so the app
// knows the request was HTTPS (otherwise the HTTPS redirect below would loop). The client IP isn't used for anything.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365));

// Outside local development the site is only served over HTTPS (Azure terminates TLS in front of the app), so the
// session cookies are always marked Secure rather than trusting the scheme of the request that reaches the app.
var cookieSecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

builder.Services.AddAuthentication(AuthSchemes.Admin)
    .AddCookie(AuthSchemes.Admin, options =>
    {
        options.Cookie.Name = "DlfVotingAdminAuth";
        options.ExpireTimeSpan = SessionValidation.Lifetime;
        options.SlidingExpiration = false;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = cookieSecurePolicy;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = SessionValidation.ValidateAdminAsync;
    })
    .AddCookie(AuthSchemes.User, options =>
    {
        options.Cookie.Name = "DlfVotingUserAuth";
        options.ExpireTimeSpan = SessionValidation.Lifetime;
        options.SlidingExpiration = false;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = cookieSecurePolicy;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = SessionValidation.ValidateUserAsync;
    });

builder.Services.AddAuthorization();

// No CORS: the website and the API are served from the same address (in production by this app, locally through
// the Vite dev server's /api proxy), so browsers never make cross-origin calls to the API.

var app = builder.Build();

await StartupTasks.RunAsync(app);

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
}

app.UseSecurityHeaders();
app.UseHttpsRedirection();

// The built React app (copied into wwwroot by dotnet publish). File names under /assets contain a content hash,
// so browsers may cache them for good; index.html is revalidated so a deploy is picked up straight away.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (context.Context.Request.Path.StartsWithSegments("/assets"))
        {
            context.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }
    }
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/healthz");

// Unknown API routes are a plain 404; every other unknown path is a page of the React app (e.g. /welcome).
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache"
});

app.Run();

// ReSharper disable once ClassNeverInstantiated.Global
public partial class Program;
