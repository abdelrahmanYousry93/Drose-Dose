using Dose.Application.Bookings;
using Dose.Application.Sessions;
using Dose.Infrastructure.Persistence;
using Hangfire;
using Hangfire.AspNetCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dose.Api;

/// <summary>
/// Prepares the database (migrations, templates, roles, first admin, optional sample data) and then starts the
/// background jobs — in the background, retrying every minute while the database is unreachable.
/// The web app therefore always starts and keeps serving the site and a clear "temporarily unavailable" answer
/// instead of crashing in a loop (which made the host show its own error page for every request).
/// </summary>
public sealed class DatabaseStartup(
    IServiceProvider services, IConfiguration config, IWebHostEnvironment env, ILogger<DatabaseStartup> log) : BackgroundService
{
    static readonly TimeSpan RetryEvery = TimeSpan.FromMinutes(1);
    BackgroundJobServer? jobServer;

    /// <summary>True once the database start-up steps have completed in this process.</summary>
    public bool Ready { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        for (var attempt = 2; !Ready && !stop.IsCancellationRequested; attempt++)
        {
            await Task.Delay(RetryEvery, stop);
            try
            {
                await PrepareDatabaseAsync(stop);
                Ready = true;
                log.LogInformation("Database ready (attempt {Attempt})", attempt);
                break;
            }
            catch (Exception ex) when (!stop.IsCancellationRequested)
            {
                log.LogError(ex, "Database not reachable yet (attempt {Attempt}); retrying in {Delay}", attempt, RetryEvery);
            }
        }

        if (stop.IsCancellationRequested || !config.GetValue("Jobs:Enabled", true)) return;
        try
        {
            StartJobs();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Background jobs could not start");
        }
    }

    /// <summary>First attempt, awaited before the app starts listening so a healthy start behaves exactly as before.</summary>
    public async Task TryFirstAsync(CancellationToken ct)
    {
        try
        {
            await PrepareDatabaseAsync(ct);
            Ready = true;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Database not reachable at start-up; the site starts anyway and keeps retrying every {Delay}", RetryEvery);
        }
    }

    async Task PrepareDatabaseAsync(CancellationToken ct)
    {
        // Development: always migrate + seed SAMPLE data. Production: migrate only when Database__MigrateOnStartup=true,
        // SAMPLE data only when Seed__SampleData=true. Roles always exist; the first admin comes from
        // Seed__AdminEmail / Seed__AdminPassword when that account does not exist yet (never changed afterwards).
        var isDev = env.IsDevelopment();
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DoseDbContext>();
        if (isDev || config.GetValue("Database:MigrateOnStartup", false))
            await db.Database.MigrateAsync(ct);
        await Dose.Application.Notifications.TemplateDefaults.EnsureAsync(db);
        await DevelopmentSeeder.SeedRolesAndAdminAsync(
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
            scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>(),
            config["Seed:AdminEmail"], config["Seed:AdminPassword"]);
        if (config.GetValue("Seed:SampleData", isDev))
        {
            await DevelopmentSeeder.SeedSampleDataAsync(db, DateTime.UtcNow);
            await DevelopmentSeeder.FillSampleEnglishAsync(db);
            await DevelopmentSeeder.FillSampleSchedulingAsync(db, DateTime.UtcNow);
        }
    }

    void StartJobs()
    {
        var storage = services.GetRequiredService<JobStorage>();
        jobServer = new BackgroundJobServer(new BackgroundJobServerOptions
        {
            WorkerCount = 2,
            Activator = new AspNetCoreJobActivator(services.GetRequiredService<IServiceScopeFactory>()),
        }, storage);

        // All jobs are idempotent.
        var jobs = services.GetRequiredService<IRecurringJobManager>();
        jobs.AddOrUpdate<GroupDecisionService>("group-tier-decisions", s => s.RunDueDecisionsAsync(CancellationToken.None), "*/5 * * * *");
        jobs.AddOrUpdate<Dose.Application.Notifications.NotificationService>("queue-session-reminders", s => s.QueueRemindersAsync(CancellationToken.None), "*/15 * * * *");
        jobs.AddOrUpdate<BookingMaintenance>("expire-stale-bookings", s => s.ExpireStaleAsync(CancellationToken.None), "*/10 * * * *");
        jobs.AddOrUpdate<Dose.Application.Payments.ProofRetention>("delete-old-payment-screenshots", s => s.PurgeAsync(CancellationToken.None), "30 3 * * *");
        jobs.AddOrUpdate<MarketingPublisher>("publish-marketing-posts", s => s.RunAsync(CancellationToken.None), "*/5 * * * *");
        log.LogInformation("Background jobs started");
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        await base.StopAsync(ct);
        jobServer?.SendStop();
        jobServer?.Dispose();
    }
}
