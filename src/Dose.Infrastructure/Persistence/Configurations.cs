using Dose.Domain.Administration;
using Dose.Domain.Bookings;
using Dose.Domain.Catalog;
using Dose.Domain.Pricing;
using Dose.Domain.Sessions;
using Dose.Domain.Teachers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dose.Infrastructure.Persistence;

internal sealed class GradeConfig : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> e)
    {
        e.HasOne(x => x.Stage).WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.PricingBand).WithMany().HasForeignKey(x => x.PricingBandId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class GradeSubjectConfig : IEntityTypeConfiguration<GradeSubject>
{
    public void Configure(EntityTypeBuilder<GradeSubject> e)
    {
        e.HasIndex(x => new { x.GradeId, x.SubjectId }).IsUnique();
        e.HasOne<Grade>().WithMany().HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ChapterConfig : IEntityTypeConfiguration<Chapter>
{
    public void Configure(EntityTypeBuilder<Chapter> e)
    {
        e.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Grade>().WithMany().HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<StudyTrack>().WithMany().HasForeignKey(x => x.StudyTrackId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class LessonConfig : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> e) =>
        e.HasOne(x => x.Chapter).WithMany().HasForeignKey(x => x.ChapterId).OnDelete(DeleteBehavior.Restrict);
}

internal sealed class ServiceTypeConfig : IEntityTypeConfiguration<ServiceType>
{
    public void Configure(EntityTypeBuilder<ServiceType> e)
    {
        e.Property(x => x.Code).HasMaxLength(50);
        e.HasIndex(x => x.Code).IsUnique();
        e.Property(x => x.DescriptionAr).HasMaxLength(1000);
        e.Property(x => x.DescriptionEn).HasMaxLength(1000);
    }
}

internal sealed class TeacherConfig : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> e)
    {
        e.Property(x => x.BioAr).HasMaxLength(2000);
        e.Property(x => x.QualificationsAr).HasMaxLength(2000);
        e.Property(x => x.BioEn).HasMaxLength(2000);
        e.Property(x => x.QualificationsEn).HasMaxLength(2000);
        e.Property(x => x.PhotoFileKey).HasMaxLength(200);
        e.Property(x => x.PhotoContentType).HasMaxLength(50);
        e.Property(x => x.UserId).HasMaxLength(450);
        e.HasIndex(x => x.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
    }
}

internal sealed class PricingPlanConfig : IEntityTypeConfiguration<PricingPlan>
{
    public void Configure(EntityTypeBuilder<PricingPlan> e)
    {
        e.Property(x => x.Currency).HasMaxLength(3).IsFixedLength();
        e.HasOne<ServiceType>().WithMany().HasForeignKey(x => x.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<PricingBand>().WithMany().HasForeignKey(x => x.PricingBandId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<StudyTrack>().WithMany().HasForeignKey(x => x.StudyTrackId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Versions).WithOne(v => v.Plan).HasForeignKey(v => v.PlanId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.ServiceTypeId, x.Mode, x.PricingBandId, x.StudyTrackId, x.SubjectId, x.IsActive });
        e.Ignore(x => x.Specificity);
        e.Ignore(x => x.CurrentVersion);
    }
}

internal sealed class PricingPlanVersionConfig : IEntityTypeConfiguration<PricingPlanVersion>
{
    public void Configure(EntityTypeBuilder<PricingPlanVersion> e)
    {
        e.HasIndex(x => new { x.PlanId, x.VersionNo }).IsUnique();
        e.HasMany(x => x.Tiers).WithOne().HasForeignKey(t => t.VersionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PricingTierConfig : IEntityTypeConfiguration<PricingTier>
{
    public void Configure(EntityTypeBuilder<PricingTier> e)
    {
        e.ToTable(t =>
        {
            t.HasCheckConstraint("CK_PricingTier_Range", "[MinParticipants] >= 1 AND [MaxParticipants] >= [MinParticipants]");
            t.HasCheckConstraint("CK_PricingTier_Price", "[PricePerStudent] > 0");
        });
    }
}

internal sealed class PriceSnapshotConfig : IEntityTypeConfiguration<PriceSnapshot>
{
    public void Configure(EntityTypeBuilder<PriceSnapshot> e)
    {
        e.Property(x => x.Currency).HasMaxLength(3).IsFixedLength();
        e.HasOne<PricingPlanVersion>().WithMany().HasForeignKey(x => x.PlanVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SessionConfig : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> e)
    {
        e.Property(x => x.RowVersion).IsRowVersion();
        e.Property(x => x.ScopeDetailsAr).HasMaxLength(2000);
        e.Property(x => x.ScopeDetailsEn).HasMaxLength(2000);
        e.Property(x => x.MeetingUrl).HasMaxLength(500);
        e.Property(x => x.MeetingProvider).HasMaxLength(50);
        e.HasOne(x => x.ServiceType).WithMany().HasForeignKey(x => x.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Grade).WithMany().HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.StudyTrack).WithMany().HasForeignKey(x => x.StudyTrackId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Chapter).WithMany().HasForeignKey(x => x.ChapterId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Lesson).WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.PricingPlanVersion).WithMany().HasForeignKey(x => x.PricingPlanVersionId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.TargetTier).WithMany().HasForeignKey(x => x.TargetTierId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.Status, x.StartUtc });
        e.HasIndex(x => new { x.GradeId, x.SubjectId, x.StudyTrackId, x.Status });
        e.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Session_Time", "[EndUtc] > [StartUtc]");
            t.HasCheckConstraint("CK_Session_Capacity", "[Capacity] >= 1 AND [MinParticipants] >= 1 AND [MinParticipants] <= [Capacity]");
        });
    }
}

internal sealed class StudentConfig : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> e)
    {
        e.Property(x => x.Name).HasMaxLength(100);
        e.Property(x => x.GuardianName).HasMaxLength(100);
        e.Property(x => x.ContactPhoneE164).HasMaxLength(16);
        e.Property(x => x.GuardianPhoneE164).HasMaxLength(16);
        e.HasIndex(x => x.ContactPhoneE164);
    }
}

internal sealed class BookingConfig : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> e)
    {
        e.Property(x => x.RowVersion).IsRowVersion();
        e.Property(x => x.Language).HasMaxLength(5);
        e.Property(x => x.Reference).HasMaxLength(16);
        e.HasIndex(x => x.Reference).IsUnique();
        e.Property(x => x.AccessTokenHash).HasMaxLength(32).IsFixedLength();
        e.Property(x => x.AccessTokenProtected).HasMaxLength(1000);
        e.HasIndex(x => x.AccessTokenHash).IsUnique();
        e.Property(x => x.Currency).HasMaxLength(3).IsFixedLength();
        e.Property(x => x.ContactPhoneE164).HasMaxLength(16);
        e.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.PriceSnapshot).WithMany().HasForeignKey(x => x.PriceSnapshotId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.SessionId, x.Status });

        // One active (seat-occupying) booking per WhatsApp number per session — enforced by the database.
        var occupying = string.Join(",", BookingStatuses.SeatOccupying.Select(s => (int)s));
        e.HasIndex(x => new { x.SessionId, x.ContactPhoneE164 })
            .IsUnique()
            .HasDatabaseName("UX_Bookings_ActivePerContact")
            .HasFilter($"[ContactPhoneE164] IS NOT NULL AND [Status] IN ({occupying})");
    }
}

internal sealed class PaymentMethodConfig : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> e)
    {
        e.Property(x => x.Code).HasMaxLength(50);
        e.HasIndex(x => x.Code).IsUnique();
        e.Property(x => x.InstructionsAr).HasMaxLength(1000);
        e.Property(x => x.InstructionsEn).HasMaxLength(1000);
    }
}

internal sealed class PaymentProofConfig : IEntityTypeConfiguration<PaymentProof>
{
    public void Configure(EntityTypeBuilder<PaymentProof> e)
    {
        e.Property(x => x.Currency).HasMaxLength(3).IsFixedLength();
        e.Property(x => x.FileContentType).HasMaxLength(50);
        e.Property(x => x.SenderReference).HasMaxLength(100);
        e.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.PaymentMethod).WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Adjustment).WithMany().HasForeignKey(x => x.AdjustmentId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.Status, x.SubmittedAtUtc });
        e.HasIndex(x => x.SenderReference);
    }
}

internal sealed class SystemSettingConfig : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> e)
    {
        e.HasKey(x => x.Key);
        e.Property(x => x.Key).HasMaxLength(100);
        e.Property(x => x.Value).HasMaxLength(1000);
    }
}

internal sealed class AuditLogConfig : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> e)
    {
        e.Property(x => x.DataJson).HasMaxLength(4000);
        e.HasIndex(x => new { x.Entity, x.EntityId });
    }
}

internal sealed class FinancialAdjustmentConfig : IEntityTypeConfiguration<FinancialAdjustment>
{
    public void Configure(EntityTypeBuilder<FinancialAdjustment> e)
    {
        e.Property(x => x.Currency).HasMaxLength(3).IsFixedLength();
        e.Property(x => x.Reason).HasMaxLength(500);
        e.Property(x => x.ResolutionNote).HasMaxLength(500);
        e.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.BookingId, x.Type, x.Status });
        e.HasIndex(x => new { x.Type, x.Status });
        e.ToTable(t => t.HasCheckConstraint("CK_Adjustment_Amount", "[Amount] >= 0"));
    }
}

internal sealed class SessionRequestConfig : IEntityTypeConfiguration<SessionRequest>
{
    public void Configure(EntityTypeBuilder<SessionRequest> e)
    {
        e.Property(x => x.Reference).HasMaxLength(16);
        e.HasIndex(x => x.Reference).IsUnique();
        e.Property(x => x.StudentName).HasMaxLength(100);
        e.Property(x => x.GuardianName).HasMaxLength(100);
        e.Property(x => x.ContactPhoneE164).HasMaxLength(16);
        e.Property(x => x.GuardianPhoneE164).HasMaxLength(16);
        e.Property(x => x.ScopeText).HasMaxLength(1000);
        e.Property(x => x.Notes).HasMaxLength(1000);
        e.Property(x => x.AdminNotes).HasMaxLength(1000);
        e.Property(x => x.Language).HasMaxLength(5);
        e.Property(x => x.PreferredDays).HasMaxLength(200);
        e.Property(x => x.PreferredTimes).HasMaxLength(200);
        e.HasOne<Grade>().WithMany().HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<StudyTrack>().WithMany().HasForeignKey(x => x.StudyTrackId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ServiceType>().WithMany().HasForeignKey(x => x.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Session>().WithMany().HasForeignKey(x => x.FulfilledBySessionId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        e.HasIndex(x => x.ContactPhoneE164);
    }
}

internal sealed class TeacherAvailabilityConfig : IEntityTypeConfiguration<TeacherAvailability>
{
    public void Configure(EntityTypeBuilder<TeacherAvailability> e)
    {
        e.Property(x => x.Note).HasMaxLength(300);
        e.Property(x => x.ReviewNote).HasMaxLength(300);
        e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.TeacherId, x.StartUtc });
        e.HasIndex(x => new { x.Status, x.StartUtc });
        e.ToTable(t => t.HasCheckConstraint("CK_Availability_Time", "[EndUtc] > [StartUtc]"));
    }
}

internal sealed class AttendanceConfig : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> e)
    {
        e.HasOne<Booking>().WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.BookingId).IsUnique();
    }
}

internal sealed class MessageTemplateConfig : IEntityTypeConfiguration<Dose.Domain.Notifications.MessageTemplate>
{
    public void Configure(EntityTypeBuilder<Dose.Domain.Notifications.MessageTemplate> e)
    {
        e.HasKey(x => x.Code);
        e.Property(x => x.Code).HasMaxLength(50);
        e.Property(x => x.TitleAr).HasMaxLength(100);
        e.Property(x => x.TitleEn).HasMaxLength(100);
        e.Property(x => x.BodyAr).HasMaxLength(2000);
        e.Property(x => x.BodyEn).HasMaxLength(2000);
    }
}

internal sealed class NotificationConfig : IEntityTypeConfiguration<Dose.Domain.Notifications.Notification>
{
    public void Configure(EntityTypeBuilder<Dose.Domain.Notifications.Notification> e)
    {
        e.Property(x => x.TemplateCode).HasMaxLength(50);
        e.Property(x => x.RecipientName).HasMaxLength(100);
        e.Property(x => x.RecipientPhoneE164).HasMaxLength(16);
        e.Property(x => x.GuardianPhoneE164).HasMaxLength(16);
        e.Property(x => x.Language).HasMaxLength(5);
        e.Property(x => x.Body).HasMaxLength(3000);
        e.Property(x => x.DedupKey).HasMaxLength(150);
        e.HasIndex(x => x.DedupKey).IsUnique();
        e.HasIndex(x => new { x.Status, x.DueAtUtc });
        e.HasIndex(x => x.BookingId);
        e.HasOne<Booking>().WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<SessionRequest>().WithMany().HasForeignKey(x => x.SessionRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
