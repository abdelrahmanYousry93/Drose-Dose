using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Dose.Api;
using Dose.Application;
using Dose.Infrastructure;
using Dose.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOptions<PublicSettings>().Bind(builder.Configuration.GetSection(PublicSettings.SectionName));

builder.Services.AddDoseApplication();
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
    .AddSignInManager();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "dose.auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Endpoints.AdminPolicy, p => p.RequireRole(DevelopmentSeeder.AdminRole));

// ----- Rate limiting for public booking and login endpoints (per client IP) -----
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(Endpoints.BookingRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy(Endpoints.LoginRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddOpenApi();

var app = builder.Build();

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
    await DevelopmentSeeder.SeedRolesAndAdminAsync(
        scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
        scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>(),
        app.Configuration["Seed:AdminEmail"], app.Configuration["Seed:AdminPassword"]);
    if (app.Configuration.GetValue("Seed:SampleData", true))
        await DevelopmentSeeder.SeedSampleDataAsync(db, DateTime.UtcNow);
}

app.MapDoseEndpoints();
app.Run();

public partial class Program;
