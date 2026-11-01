using Dose.Domain.Administration;
using Dose.Domain.Bookings;
using Dose.Domain.Catalog;
using Dose.Domain.Pricing;
using Dose.Domain.Sessions;
using Dose.Domain.Teachers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Dose.Application.Abstractions;

public interface IDoseDbContext
{
    DbSet<EducationalStage> EducationalStages { get; }
    DbSet<StudyTrack> StudyTracks { get; }
    DbSet<PricingBand> PricingBands { get; }
    DbSet<Grade> Grades { get; }
    DbSet<Subject> Subjects { get; }
    DbSet<GradeSubject> GradeSubjects { get; }
    DbSet<Chapter> Chapters { get; }
    DbSet<Lesson> Lessons { get; }
    DbSet<ServiceType> ServiceTypes { get; }
    DbSet<Teacher> Teachers { get; }
    DbSet<PricingPlan> PricingPlans { get; }
    DbSet<PricingPlanVersion> PricingPlanVersions { get; }
    DbSet<PricingTier> PricingTiers { get; }
    DbSet<PriceSnapshot> PriceSnapshots { get; }
    DbSet<Session> Sessions { get; }
    DbSet<Student> Students { get; }
    DbSet<Booking> Bookings { get; }
    DbSet<PaymentMethod> PaymentMethods { get; }
    DbSet<PaymentProof> PaymentProofs { get; }
    DbSet<SystemSetting> SystemSettings { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<FinancialAdjustment> FinancialAdjustments { get; }
    DbSet<SessionRequest> SessionRequests { get; }

    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Takes an exclusive update lock on the session row for the current transaction,
    /// serializing all seat operations on that session. Must be called inside a transaction.
    /// </summary>
    Task LockSessionAsync(Guid sessionId, CancellationToken ct);

    /// <summary>True when the exception is a unique-index violation (e.g. duplicate active booking).</summary>
    bool IsUniqueViolation(Exception ex);
}

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string extension, CancellationToken ct);
    Task<Stream?> OpenAsync(string key, CancellationToken ct);
}
