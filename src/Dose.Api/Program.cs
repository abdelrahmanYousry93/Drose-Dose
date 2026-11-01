using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Dose.Api;
using Dose.Application;
using Dose.Infrastructure;
using Dose.Application.Bookings;
using Dose.Application.Sessions;
using Dose.Infrastructure.Persistence;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddDoseSecurity();
if (!builder.Environment.IsDevelopment())
{
    // Structured JSON logs for Azure; request bodies, phone numbers and tokens are never logged.
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(o => o.UseUtcTimestamp = true);
}

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOptions<PublicSettings>().Bind(builder.Configuration.GetSection(PublicSettings.SectionName));

builder.Services.AddDoseApplication();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<Dose.Application.Abstractions.IAppLinks, AppLinks>();
builder.Services.AddScoped<Dose.Application.Abstractions.IRequestLanguage, HttpRequestLanguage>();
builder.Services.AddDoseInfrastructure(builder.Configuration);

// ----- Admin / teacher authentication: Identity with an HTTP-only, same-site cookie -----
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.AddIdentityCore<IdentityUser>(o =>
    {
        o.Password.RequiredLength = 10;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<DoseDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "dose.auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Endpoints.AdminPolicy, p => p.RequireRole(DevelopmentSeeder.AdminRole))
    .AddPolicy(Endpoints.TeacherPolicy, p => p.RequireRole(DevelopmentSeeder.TeacherRole));
// Disabled accounts and password resets take effect within a minute.
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));
builder.Services.AddScoped<Dose.Infrastructure.Identity.UserAdminService>();

// ----- Rate limiting for public booking and login endpoints (per client IP) -----
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddGlobalLimiter();
    o.AddPolicy(Endpoints.BookingRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
        Security.ClientKey(http),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy(Endpoints.LoginRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
        Security.ClientKey(http),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddOpenApi();

// ----- Background jobs (Hangfire, SQL Server storage). All jobs are idempotent. -----
var jobsEnabled = builder.Configuration.GetValue("Jobs:Enabled", true);
if (jobsEnabled)
{
    builder.Services.AddHangfire(c => c
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(builder.Configuration.GetConnectionString("Dose")));
    builder.Services.AddHangfireServer(o => o.WorkerCount = 2);
}

var app = builder.Build();

app.UseDoseSecurity();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<DoseDbContext>();
    await db.Database.MigrateAsync();
    await Dose.Application.Notifications.TemplateDefaults.EnsureAsync(db);
    await DevelopmentSeeder.SeedRolesAndAdminAsync(
        scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
        scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>(),
        app.Configuration["Seed:AdminEmail"], app.Configuration["Seed:AdminPassword"]);
    if (app.Configuration.GetValue("Seed:SampleData", true))
    {
        await DevelopmentSeeder.SeedSampleDataAsync(db, DateTime.UtcNow);
        await DevelopmentSeeder.FillSampleEnglishAsync(db);
    }
}
else
{
    // Production: schema is migrated by the deployment step; default message templates are inserted once if missing.
    await using var scope = app.Services.CreateAsyncScope();
    await Dose.Application.Notifications.TemplateDefaults.EnsureAsync(scope.ServiceProvider.GetRequiredService<DoseDbContext>());
}

app.MapDoseEndpoints();
app.MapAdminConfigEndpoints();
app.MapPhase3Endpoints();
app.MapPhase4Endpoints();
app.MapMessagingEndpoints();
app.MapDoseSpa();

if (jobsEnabled)
{
    var jobs = app.Services.GetRequiredService<IRecurringJobManager>();
    jobs.AddOrUpdate<GroupDecisionService>("group-tier-decisions", s => s.RunDueDecisionsAsync(CancellationToken.None), "*/5 * * * *");
    jobs.AddOrUpdate<Dose.Application.Notifications.NotificationService>("queue-session-reminders", s => s.QueueRemindersAsync(CancellationToken.None), "*/15 * * * *");
    jobs.AddOrUpdate<BookingMaintenance>("expire-stale-bookings", s => s.ExpireStaleAsync(CancellationToken.None), "*/10 * * * *");
    jobs.AddOrUpdate<Dose.Application.Payments.ProofRetention>("delete-old-payment-screenshots", s => s.PurgeAsync(CancellationToken.None), "30 3 * * *");
}
app.Run();

public partial class Program;
