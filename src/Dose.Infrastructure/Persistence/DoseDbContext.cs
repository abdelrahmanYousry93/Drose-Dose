using Dose.Application.Abstractions;
using Dose.Domain.Administration;
using Dose.Domain.Bookings;
using Dose.Domain.Catalog;
using Dose.Domain.Pricing;
using Dose.Domain.Sessions;
using Dose.Domain.Teachers;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Dose.Infrastructure.Persistence;

public sealed class DoseDbContext(DbContextOptions<DoseDbContext> options)
    : IdentityDbContext<IdentityUser>(options), IDoseDbContext, IDataProtectionKeyContext
{
    /// <summary>Encryption keys for login cookies and booking links — kept in the database so they survive restarts and redeploys.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<EducationalStage> EducationalStages => Set<EducationalStage>();
    public DbSet<StudyTrack> StudyTracks => Set<StudyTrack>();
    public DbSet<PricingBand> PricingBands => Set<PricingBand>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<GradeSubject> GradeSubjects => Set<GradeSubject>();
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<PricingPlan> PricingPlans => Set<PricingPlan>();
    public DbSet<PricingPlanVersion> PricingPlanVersions => Set<PricingPlanVersion>();
    public DbSet<PricingTier> PricingTiers => Set<PricingTier>();
    public DbSet<PriceSnapshot> PriceSnapshots => Set<PriceSnapshot>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<PaymentProof> PaymentProofs => Set<PaymentProof>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<FinancialAdjustment> FinancialAdjustments => Set<FinancialAdjustment>();
    public DbSet<SessionRequest> SessionRequests => Set<SessionRequest>();
    public DbSet<TeacherAvailability> TeacherAvailabilities => Set<TeacherAvailability>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Domain.Notifications.MessageTemplate> MessageTemplates => Set<Domain.Notifications.MessageTemplate>();
    public DbSet<Domain.Notifications.Notification> Notifications => Set<Domain.Notifications.Notification>();
    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();
    public DbSet<TeacherSubject> TeacherSubjects => Set<TeacherSubject>();
    public DbSet<Domain.Administration.MarketingPost> MarketingPosts => Set<Domain.Administration.MarketingPost>();

    public Task LockSessionAsync(Guid sessionId, CancellationToken ct)
    {
        if (Database.CurrentTransaction is null)
            throw new InvalidOperationException("LockSessionAsync must be called inside a transaction.");
        return Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM [Sessions] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {sessionId}", ct);
    }

    public Task LockPromoAsync(Guid promoId, CancellationToken ct)
    {
        if (Database.CurrentTransaction is null) throw new InvalidOperationException("LockPromoAsync must be called inside a transaction.");
        return Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM [PromoCodes] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {promoId}", ct);
    }

    public Task LockTeacherAsync(Guid teacherId, CancellationToken ct)
    {
        if (Database.CurrentTransaction is null) throw new InvalidOperationException("LockTeacherAsync must be called inside a transaction.");
        return Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM [Teachers] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {teacherId}", ct);
    }

    public bool IsUniqueViolation(Exception ex) =>
        ex.GetBaseException() is SqlException { Number: 2601 or 2627 };

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<decimal>().HavePrecision(12, 2);
        b.Properties<string>().HaveMaxLength(400);
        b.Properties<DateTime>().HaveColumnType("datetime2").HaveConversion<UtcDateTimeConverter>();
        b.Properties<DateTime?>().HaveColumnType("datetime2").HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.ApplyConfigurationsFromAssembly(typeof(DoseDbContext).Assembly);
        b.Entity<DataProtectionKey>().Property(k => k.Xml).HasColumnType("nvarchar(max)").HasMaxLength(-1);
    }
}

/// <summary>All timestamps are stored as UTC; this marks values read back as UTC so they serialize with "Z".</summary>
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

internal sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
    v => v.HasValue ? (v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)) : v,
    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
