using DlfVoting.Api;
using DlfVoting.Api.Imports;
using DlfVoting.Api.Services;
using DlfVoting.Infrastructure;
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

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontendDev", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<DlfVotingDbContext>();
await VotingSettingsSeeder.SeedAsync(db);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    
    await AdminSeeder.SeedDefaultAdminAsync(db);
    await UserSeeder.SeedDevUsersAsync(db);
}

app.UseCors("AllowFrontendDev");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// ReSharper disable once ClassNeverInstantiated.Global
public partial class Program;