using System.Text.Json;
using Dose.Application.Abstractions;
using Dose.Domain.Administration;
using Dose.Domain.Common;

namespace Dose.Application.Administration;

public static class AdminErrors
{
    public const string Invalid = "admin.invalid";
    public const string NotFound = "admin.not_found";
    public const string Conflict = "admin.conflict";
}

/// <summary>Bilingual labels used in admin validation messages.</summary>
internal static class Fields
{
    public static readonly LocalizedText Name = new("الاسم", "Name");
    public static readonly LocalizedText Stage = new("المرحلة", "Stage");
    public static readonly LocalizedText StudyTrack = new("المنهج", "Curriculum track");
    public static readonly LocalizedText PricingBand = new("شريحة التسعير", "Pricing band");
    public static readonly LocalizedText Subject = new("المادة", "Subject");
    public static readonly LocalizedText Grade = new("الصف", "Grade");
    public static readonly LocalizedText Chapter = new("الوحدة", "Chapter");
    public static readonly LocalizedText ChapterName = new("اسم الوحدة", "Chapter name");
    public static readonly LocalizedText Lesson = new("الدرس", "Lesson");
    public static readonly LocalizedText LessonName = new("اسم الدرس", "Lesson name");
    public static readonly LocalizedText ServiceType = new("نوع الخدمة", "Service type");
    public static readonly LocalizedText Teacher = new("المدرس", "Teacher");
    public static readonly LocalizedText TeacherName = new("اسم المدرس", "Teacher name");
    public static readonly LocalizedText PaymentMethod = new("طريقة الدفع", "Payment method");
    public static readonly LocalizedText ReceivingAccount = new("رقم أو عنوان الاستلام", "Receiving account");
    public static readonly LocalizedText PricingPlan = new("خطة التسعير", "Pricing plan");
    public static readonly LocalizedText PlanName = new("اسم الخطة", "Plan name");
    public static readonly LocalizedText TierName = new("اسم الشريحة", "Group size name");
    public static readonly LocalizedText Session = new("الجلسة", "Session");
    public static readonly LocalizedText SessionTitle = new("عنوان الجلسة", "Session title");
    public static readonly LocalizedText CancelReason = new("سبب الإلغاء", "Cancellation reason");
    public static readonly LocalizedText Booking = new("الحجز", "Booking");
    public static readonly LocalizedText Request = new("الطلب", "Request");
    public static readonly LocalizedText StudentName = new("اسم الطالب", "Student name");
    public static readonly LocalizedText Scope = new("الجزء المطلوب شرحه", "What you need explained");
    public static readonly LocalizedText Template = new("القالب", "Template");
    public static readonly LocalizedText MessageBody = new("نص الرسالة", "Message text");
    public static readonly LocalizedText Message = new("الرسالة", "Message");
    public static readonly LocalizedText Promo = new("كود الخصم", "Promo code");
}

internal static class AdminGuard
{
    public static string Name(string? value, int max = 100) => Name(value, Fields.Name, max);

    public static string Name(string? value, LocalizedText field, int max = 100)
    {
        var v = (value ?? "").Trim();
        if (v.Length < 2 || v.Length > max)
            throw new DomainException(AdminErrors.Invalid, $"{field.Ar} لازم يكون بين 2 و{max} حرف.", $"{field.En} must be between 2 and {max} characters.");
        return v;
    }

    public static string? Optional(string? value, int max)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        if (v.Length > max) throw new DomainException(AdminErrors.Invalid, $"النص أطول من {max} حرف.", $"The text is longer than {max} characters.");
        return v;
    }

    public static string? Url(string? value)
    {
        var v = Optional(value, 500);
        if (v is null) return null;
        if (!Uri.TryCreate(v, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps)
            throw new DomainException(AdminErrors.Invalid, "الرابط لازم يكون https كامل.", "The link must be a full https URL.");
        return v;
    }

    public static T Found<T>(T? entity, LocalizedText what) where T : class =>
        entity ?? throw new DomainException(AdminErrors.NotFound, $"{what.Ar} مش موجود.", $"{what.En} not found.");

    public static void Audit(this IDoseDbContext db, IClock clock, string actor, string entity, object id, string action, object? data = null)
    {
        var json = data is null ? null : JsonSerializer.Serialize(data);
        db.AuditLogs.Add(new AuditLog
        {
            Actor = actor, Entity = entity, EntityId = id.ToString()!, Action = action,
            DataJson = json is null ? null : json[..Math.Min(json.Length, 4000)],
            AtUtc = clock.UtcNow,
        });
    }
}
