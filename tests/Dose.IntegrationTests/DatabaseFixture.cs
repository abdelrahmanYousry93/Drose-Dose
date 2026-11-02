using Dose.Application.Abstractions;
using Dose.Application.Administration;
using Dose.Application.Bookings;
using Dose.Application.Payments;
using Dose.Application.Sessions;
using Dose.Application.Notifications;
using Dose.Domain.Catalog;
using Dose.Domain.Pricing;
using Dose.Domain.Sessions;
using Dose.Infrastructure.Persistence;
using Dose.Infrastructure.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Dose.IntegrationTests;

public sealed class FakeClock(DateTime start) : IClock
{
    public DateTime UtcNow { get; set; } = start;
}

/// <summary>
/// Real SQL Server database per test run (locking and filtered unique indexes can't be faked).
/// Set DOSE_TEST_SQL to a server connection string, e.g.
/// Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=True
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    public static readonly string? ServerConnection = Environment.GetEnvironmentVariable("DOSE_TEST_SQL");
    public string ConnectionString { get; private set; } = "";
    public DateTime SeedTime { get; } = new(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);
    private readonly string _dbName = $"DoseTest_{Guid.NewGuid():N}";
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "dose-test-files", Guid.NewGuid().ToString("N"));

    public async ValueTask InitializeAsync()
    {
        if (ServerConnection is null) return;
        ConnectionString = new SqlConnectionStringBuilder(ServerConnection) { InitialCatalog = _dbName }.ConnectionString;
        await using var db = NewDb();
        await db.Database.MigrateAsync();
        await DevelopmentSeeder.SeedSampleDataAsync(db, SeedTime);
        await DevelopmentSeeder.FillSampleSchedulingAsync(db, SeedTime);
        await TemplateDefaults.EnsureAsync(db);
    }

    public async ValueTask DisposeAsync()
    {
        if (ServerConnection is null) return;
        await using var db = NewDb();
        await db.Database.EnsureDeletedAsync();
        if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, true);
    }

    public DoseDbContext NewDb() =>
        new(new DbContextOptionsBuilder<DoseDbContext>().UseSqlServer(ConnectionString).Options);

    public LocalFileStorage Storage => new(_storageRoot);

    public static readonly ITokenProtector Tokens =
        new Dose.Infrastructure.Security.DataProtectionTokenProtector(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());

    public sealed class TestLinks : IAppLinks
    {
        public string BookingLink(string token) => $"https://dose.test/b/{token}";
    }

    public NotificationService Notify(DoseDbContext db, IClock clock) => new(db, clock, Tokens, new TestLinks());

    public BookingService Bookings(DoseDbContext db, IClock clock, IRequestLanguage? lang = null) =>
        new(db, clock, new SettingsService(db, clock), Storage, Decisions(db, clock), lang ?? FixedLanguage.Arabic, Tokens,
            new PromoService(db, clock), Notify(db, clock));

    public PromoService Promos(DoseDbContext db, IClock clock) => new(db, clock);

    public PrivateBookingService Private(DoseDbContext db, IClock clock, IRequestLanguage? lang = null) =>
        new(db, clock, new SettingsService(db, clock), lang ?? FixedLanguage.Arabic, Tokens, Notify(db, clock));

    public PaymentReviewService Payments(DoseDbContext db, IClock clock) =>
        new(db, clock, new SettingsService(db, clock), Storage, Decisions(db, clock), Notify(db, clock));

    public GroupDecisionService Decisions(DoseDbContext db, IClock clock) => new(db, clock, new SettingsService(db, clock), Notify(db, clock));

    public FollowUpService FollowUps(DoseDbContext db, IClock clock) => new(db, clock, Decisions(db, clock), Notify(db, clock));

    public AdminSessionService AdminSessions(DoseDbContext db, IClock clock) =>
        new(db, clock, new SettingsService(db, clock), Notify(db, clock), Tokens);

    public AdminCatalogService Catalog(DoseDbContext db, IClock clock) => new(db, clock, Storage);

    public SessionRequestService Requests(DoseDbContext db, IClock clock, IRequestLanguage? lang = null) =>
        new(db, clock, lang ?? FixedLanguage.Arabic, Notify(db, clock));

    /// <summary>Creates a fresh published session so tests don't share seats.</summary>
    public async Task<Session> CreateSessionAsync(
        SessionMode mode, string tierName = "جروب صغير (2–4)", string gradeStage = "ثانوي", int? capacity = null)
    {
        await using var db = NewDb();
        var grade = await db.Grades.Include(g => g.Stage).FirstAsync(g => g.Stage!.NameAr == gradeStage);
        var track = await db.StudyTracks.FirstAsync();
        var subject = await db.Subjects.FirstAsync();
        var service = await db.ServiceTypes.FirstAsync(s => s.Code == "one_lesson");
        var teacher = await db.Teachers.FirstAsync();
        var plan = await db.PricingPlans.Include(p => p.Versions).ThenInclude(v => v.Tiers)
            .FirstAsync(p => p.Mode == mode && p.Name.StartsWith("SAMPLE"));
        var version = plan.Versions.Single();
        PricingTier? tier = mode == SessionMode.Group ? version.Tiers.Single(t => t.NameAr == tierName) : null;

        var start = SeedTime.AddDays(5);
        var session = new Session
        {
            ScopeTitleAr = "جلسة اختبار", GradeId = grade.Id, StudyTrackId = track.Id, SubjectId = subject.Id,
            ServiceTypeId = service.Id, TeacherId = teacher.Id, StartUtc = start, EndUtc = start.AddHours(1), Mode = mode,
            Capacity = capacity ?? tier?.MaxParticipants ?? 1, MinParticipants = tier?.MinParticipants ?? 1,
            PricingPlanVersionId = version.Id, TargetTierId = tier?.Id, Policy = PricingPolicy.TargetTierWithTopUp,
            BookingDeadlineUtc = start.AddHours(-24), TierDecisionAtUtc = mode == SessionMode.Group ? start.AddHours(-24) : null,
            Status = SessionStatus.Published, MeetingProvider = "GoogleMeet", MeetingUrl = "https://meet.google.com/test-room",
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    public static async Task<Guid> PaymentMethodIdAsync(DoseDbContext db) => (await db.PaymentMethods.FirstAsync(m => m.Code == "instapay")).Id;

    public static MemoryStream TinyPng() => new(
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE,
    ]);
}

[CollectionDefinition(Name)]
public sealed class DbCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "database";
}
