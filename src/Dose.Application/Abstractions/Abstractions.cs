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
    DbSet<TeacherAvailability> TeacherAvailabilities { get; }
    DbSet<Attendance> Attendances { get; }
    DbSet<Domain.Notifications.MessageTemplate> MessageTemplates { get; }
    DbSet<Domain.Notifications.Notification> Notifications { get; }
    DbSet<PromoCode> PromoCodes { get; }
    DbSet<TeacherSubject> TeacherSubjects { get; }

    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Takes an exclusive update lock on the session row for the current transaction,
    /// serializing all seat operations on that session. Must be called inside a transaction.
    /// </summary>
    Task LockSessionAsync(Guid sessionId, CancellationToken ct);

    /// <summary>Exclusive update lock on a promo code row (enforces its use limits under concurrency). Inside a transaction.</summary>
    Task LockPromoAsync(Guid promoId, CancellationToken ct);

    /// <summary>Exclusive update lock on a teacher row (one student at a time books that teacher's free time). Inside a transaction.</summary>
    Task LockTeacherAsync(Guid teacherId, CancellationToken ct);

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
    /// <summary>Removes the file if it exists (idempotent).</summary>
    Task DeleteAsync(string key, CancellationToken ct);
}

/// <summary>The language of the current request ("ar" or "en"), resolved from the Accept-Language header.</summary>
public interface IRequestLanguage
{
    string Code { get; }
    bool IsEnglish => Code == "en";
    /// <summary>Picks the English text when requested and available, otherwise Arabic.</summary>
    string Pick(string ar, string? en) => IsEnglish && !string.IsNullOrWhiteSpace(en) ? en : ar;
}

public sealed class FixedLanguage(string code) : IRequestLanguage
{
    public static readonly FixedLanguage Arabic = new("ar");
    public static readonly FixedLanguage English = new("en");
    public string Code { get; } = code;
}

/// <summary>Encrypts booking access tokens so links can be re-sent (ASP.NET Core Data Protection in production).</summary>
public interface ITokenProtector
{
    string Protect(string token);
    string? Unprotect(string protectedToken);
}

/// <summary>Absolute links into the web app (configured base URL).</summary>
public interface IAppLinks
{
    string BookingLink(string token);
}
