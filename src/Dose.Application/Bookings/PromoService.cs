using System.Text.RegularExpressions;
using Dose.Application.Abstractions;
using Dose.Application.Administration;
using Dose.Contracts;
using Dose.Contracts.Admin;
using Dose.Domain.Bookings;
using Dose.Domain.Common;
using Dose.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Dose.Application.Bookings;

/// <summary>The discount a promo code gives on one booking.</summary>
public sealed record PromoQuote(PromoCode Promo, decimal Discount, decimal NetPrice);

/// <summary>
/// Promo codes: admin management and validation at booking. Uses are counted from bookings that still hold or paid
/// for their seat, so an expired or unpaid-cancelled booking gives the use back automatically.
/// </summary>
public sealed partial class PromoService(IDoseDbContext db, IClock clock)
{
    /// <summary>Booking statuses where the code counts as used.</summary>
    public static readonly BookingStatus[] CountingStatuses =
    [
        BookingStatus.PendingPayment, BookingStatus.PaymentUnderReview, BookingStatus.Confirmed, BookingStatus.TopUpPending,
        BookingStatus.Completed, BookingStatus.NoShow, BookingStatus.Transferred,
    ];

    [GeneratedRegex("^[A-Z0-9-]{3,30}$")]
    private static partial Regex CodePattern();

    public static string Normalize(string? code) => (code ?? "").Trim().ToUpperInvariant();

    private static DomainException Invalid(string ar, string en) => new(PromoErrors.Invalid, ar, en);

    /// <summary>
    /// Validates a code for a booking and returns the discount. Pass the phone when known (per-number limit).
    /// With <paramref name="lockRow"/> the promo row is locked for the current transaction so limits hold under load.
    /// </summary>
    public async Task<PromoQuote> QuoteAsync(string? rawCode, Session session, decimal listPrice, string? phoneE164,
        Guid? excludeBookingId, bool lockRow, CancellationToken ct)
    {
        var code = Normalize(rawCode);
        var promo = code.Length == 0 ? null : await db.PromoCodes.AsNoTracking().FirstOrDefaultAsync(p => p.Code == code, ct);
        if (promo is null || !promo.IsActive)
            throw Invalid("كود الخصم ده مش صحيح أو مش متاح.", "This promo code isn't valid or isn't available.");
        if (lockRow) await db.LockPromoAsync(promo.Id, ct);

        var now = clock.UtcNow;
        if (promo.ValidFromUtc is { } from && now < from)
            throw Invalid("كود الخصم ده لسه مبدأش.", "This promo code isn't active yet.");
        if (promo.ValidToUtc is { } to && now > to)
            throw Invalid("كود الخصم ده انتهى.", "This promo code has expired.");

        var fits = (promo.GradeId is null || promo.GradeId == session.GradeId)
                   && (promo.SubjectId is null || promo.SubjectId == session.SubjectId)
                   && (promo.ServiceTypeId is null || promo.ServiceTypeId == session.ServiceTypeId)
                   && (promo.LessonId is null || promo.LessonId == session.LessonId)
                   && (promo.SessionId is null || promo.SessionId == session.Id);
        if (!fits) throw Invalid("كود الخصم ده مش على الجلسة دي.", "This promo code doesn't apply to this session.");

        var uses = db.Bookings.Where(b => b.PromoCodeId == promo.Id && CountingStatuses.Contains(b.Status));
        if (excludeBookingId is { } ex) uses = uses.Where(b => b.Id != ex);
        if (promo.MaxTotalUses is { } max && await uses.CountAsync(ct) >= max)
            throw Invalid("كود الخصم ده خلص عدد مرات استخدامه.", "This promo code has reached its usage limit.");
        if (promo.MaxUsesPerSession is { } perSession && await uses.CountAsync(b => b.SessionId == session.Id, ct) >= perSession)
            throw Invalid("كود الخصم ده خلص عدد مرات استخدامه في الجلسة دي.", "This promo code has reached its limit for this session.");
        if (phoneE164 is not null && promo.MaxUsesPerPhone is { } perPhone
            && await uses.CountAsync(b => b.ContactPhoneE164 == phoneE164, ct) >= perPhone)
            throw Invalid("إنت استخدمت كود الخصم ده قبل كده.", "You've already used this promo code.");

        var discount = promo.Kind == PromoKind.Percent
            ? Math.Round(listPrice * promo.Value / 100, 2, MidpointRounding.AwayFromZero)
            : Math.Min(promo.Value, listPrice);
        return new PromoQuote(promo, discount, listPrice - discount);
    }

    /// <summary>Records the promo on the booking (call after <see cref="QuoteAsync"/>; the caller saves).</summary>
    public static void Apply(Booking booking, PromoQuote? quote)
    {
        booking.PromoCodeId = quote?.Promo.Id;
        booking.DiscountPercent = quote?.Promo.Kind == PromoKind.Percent ? quote.Promo.Value : null;
        booking.DiscountAmount = quote?.Discount ?? 0;
        booking.QuotedPrice = quote?.NetPrice ?? booking.ListPrice;
    }

    public static PromoPreviewDto ToPreview(PromoQuote q, string currency) =>
        new(q.Promo.Code, q.Promo.Description, q.Promo.Kind.ToString(), q.Promo.Value, q.Discount, q.NetPrice, currency);

    // ---------- Admin ----------

    public async Task<IReadOnlyList<PromoCodeAdminDto>> ListAsync(CancellationToken ct)
    {
        var promos = await db.PromoCodes.AsNoTracking().OrderByDescending(p => p.CreatedAtUtc).ToListAsync(ct);
        var ids = promos.Select(p => p.Id).ToList();
        var stats = await db.Bookings.AsNoTracking()
            .Where(b => b.PromoCodeId != null && ids.Contains(b.PromoCodeId.Value) && CountingStatuses.Contains(b.Status))
            .GroupBy(b => b.PromoCodeId!.Value)
            .Select(g => new { g.Key, Uses = g.Count(), Total = g.Sum(b => b.DiscountAmount) })
            .ToDictionaryAsync(x => x.Key, ct);

        var gradeIds = promos.Where(p => p.GradeId != null).Select(p => p.GradeId!.Value).ToList();
        var subjectIds = promos.Where(p => p.SubjectId != null).Select(p => p.SubjectId!.Value).ToList();
        var serviceIds = promos.Where(p => p.ServiceTypeId != null).Select(p => p.ServiceTypeId!.Value).ToList();
        var lessonIds = promos.Where(p => p.LessonId != null).Select(p => p.LessonId!.Value).ToList();
        var sessionIds = promos.Where(p => p.SessionId != null).Select(p => p.SessionId!.Value).ToList();
        var grades = await db.Grades.Where(x => gradeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.NameAr, ct);
        var subjects = await db.Subjects.Where(x => subjectIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.NameAr, ct);
        var services = await db.ServiceTypes.Where(x => serviceIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.NameAr, ct);
        var lessons = await db.Lessons.Where(x => lessonIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.NameAr, ct);
        var sessions = await db.Sessions.Where(x => sessionIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ScopeTitleAr, ct);

        return promos.Select(p =>
        {
            var parts = new List<string>();
            if (p.SessionId is { } s) parts.Add(sessions.GetValueOrDefault(s, "?"));
            if (p.LessonId is { } l) parts.Add(lessons.GetValueOrDefault(l, "?"));
            if (p.SubjectId is { } sub) parts.Add(subjects.GetValueOrDefault(sub, "?"));
            if (p.GradeId is { } g) parts.Add(grades.GetValueOrDefault(g, "?"));
            if (p.ServiceTypeId is { } sv) parts.Add(services.GetValueOrDefault(sv, "?"));
            var st = stats.GetValueOrDefault(p.Id);
            return new PromoCodeAdminDto(p.Id, p.Code, p.Description, p.Kind.ToString(), p.Value, p.GradeId, p.SubjectId, p.ServiceTypeId,
                p.LessonId, p.SessionId, p.ValidFromUtc, p.ValidToUtc, p.MaxTotalUses, p.MaxUsesPerPhone, p.MaxUsesPerSession, p.IsActive,
                st?.Uses ?? 0, st?.Total ?? 0, parts.Count == 0 ? null : string.Join(" · ", parts));
        }).ToList();
    }

    public async Task<Guid> SaveAsync(PromoCodeAdminDto dto, string actor, CancellationToken ct)
    {
        var code = Normalize(dto.Code);
        if (!CodePattern().IsMatch(code))
            throw new DomainException(AdminErrors.Invalid, "الكود من 3 لـ 30 حرف إنجليزي أو رقم أو شرطة (-).", "The code must be 3–30 letters, digits or dashes (-).");
        if (!Enum.TryParse<PromoKind>(dto.Kind, out var kind))
            throw new DomainException(AdminErrors.Invalid, "اختار نوع الخصم: نسبة أو مبلغ ثابت.", "Choose the discount type: percentage or fixed amount.");
        if (dto.Value <= 0 || (kind == PromoKind.Percent && dto.Value > 100) || dto.Value > 100_000)
            throw new DomainException(AdminErrors.Invalid, kind == PromoKind.Percent ? "النسبة لازم تكون من 1 لـ 100." : "المبلغ لازم يكون أكبر من صفر.",
                kind == PromoKind.Percent ? "The percentage must be between 1 and 100." : "The amount must be greater than zero.");
        if (dto.ValidFromUtc is { } f && dto.ValidToUtc is { } t && t <= f)
            throw new DomainException(AdminErrors.Invalid, "تاريخ الانتهاء لازم يكون بعد تاريخ البداية.", "The end date must be after the start date.");
        foreach (var n in new[] { dto.MaxTotalUses, dto.MaxUsesPerPhone, dto.MaxUsesPerSession })
            if (n is < 1 or > 1_000_000)
                throw new DomainException(AdminErrors.Invalid, "عدد مرات الاستخدام لازم يكون رقم موجب (أو سيبه فاضي = بلا حد).", "Usage limits must be positive numbers (or empty = no limit).");
        if (dto.SessionId is { } sid && !await db.Sessions.AnyAsync(s => s.Id == sid, ct)) throw new DomainException(AdminErrors.Invalid, "الجلسة مش موجودة.", "Session not found.");
        if (dto.LessonId is { } lid && !await db.Lessons.AnyAsync(l => l.Id == lid, ct)) throw new DomainException(AdminErrors.Invalid, "الدرس مش موجود.", "Lesson not found.");
        if (dto.SubjectId is { } subId && !await db.Subjects.AnyAsync(s => s.Id == subId, ct)) throw new DomainException(AdminErrors.Invalid, "المادة مش موجودة.", "Subject not found.");
        if (dto.GradeId is { } gid && !await db.Grades.AnyAsync(g => g.Id == gid, ct)) throw new DomainException(AdminErrors.Invalid, "الصف مش موجود.", "Grade not found.");
        if (dto.ServiceTypeId is { } svid && !await db.ServiceTypes.AnyAsync(s => s.Id == svid, ct)) throw new DomainException(AdminErrors.Invalid, "نوع الخدمة مش موجود.", "Service type not found.");

        if (await db.PromoCodes.AnyAsync(p => p.Code == code && p.Id != dto.Id, ct))
            throw new DomainException(AdminErrors.Conflict, "فيه كود تاني بنفس الاسم.", "Another promo code already uses this code.");

        var now = clock.UtcNow;
        var e = dto.Id is { } id
            ? AdminGuard.Found(await db.PromoCodes.FirstOrDefaultAsync(p => p.Id == id, ct), Fields.Promo)
            : null;
        if (e is null)
        {
            e = new PromoCode { Code = code, CreatedBy = actor, CreatedAtUtc = now };
            db.PromoCodes.Add(e);
        }
        e.Code = code;
        e.Description = AdminGuard.Optional(dto.Description, 300);
        e.Kind = kind;
        e.Value = Math.Round(dto.Value, 2);
        e.GradeId = dto.GradeId;
        e.SubjectId = dto.SubjectId;
        e.ServiceTypeId = dto.ServiceTypeId;
        e.LessonId = dto.LessonId;
        e.SessionId = dto.SessionId;
        e.ValidFromUtc = dto.ValidFromUtc is { } vf ? DateTime.SpecifyKind(vf, DateTimeKind.Utc) : null;
        e.ValidToUtc = dto.ValidToUtc is { } vt ? DateTime.SpecifyKind(vt, DateTimeKind.Utc) : null;
        e.MaxTotalUses = dto.MaxTotalUses;
        e.MaxUsesPerPhone = dto.MaxUsesPerPhone;
        e.MaxUsesPerSession = dto.MaxUsesPerSession;
        e.IsActive = dto.IsActive;
        db.Audit(clock, actor, nameof(PromoCode), e.Id, dto.Id is null ? "Create" : "Update", dto);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }
}
