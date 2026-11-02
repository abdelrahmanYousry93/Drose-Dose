using System.Globalization;
using System.Text.RegularExpressions;
using Dose.Application.Abstractions;
using Dose.Application.Administration;
using Dose.Domain.Bookings;
using Dose.Domain.Common;
using Dose.Domain.Notifications;
using Dose.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Dose.Application.Notifications;

public sealed record NotificationDto(
    Guid Id, string TemplateCode, string TemplateTitle, string Status, string RecipientName, string RecipientPhone,
    string? GuardianPhone, string Language, string Preview, string? BookingReference, DateTime CreatedAtUtc, DateTime DueAtUtc,
    DateTime? HandledAtUtc, string? HandledBy);
public sealed record ComposedMessage(string Text, string ChatUrl, string? GuardianChatUrl);
public sealed record TemplateDto(string Code, string TitleAr, string TitleEn, string BodyAr, string BodyEn, bool IsActive, IReadOnlyList<string> Placeholders);
public sealed record SaveTemplateRequest(string BodyAr, string BodyEn, bool IsActive);

/// <summary>
/// Message templates and the outbox/log (spec 10). Events queue messages; in the MVP a team member sends them on
/// WhatsApp and marks them sent. The secret booking link is never stored in the log — "{link}" is filled in only
/// when the message is opened for sending. A future WhatsApp Cloud API provider can consume the same queue.
/// </summary>
public sealed class NotificationService(IDoseDbContext db, IClock clock, ITokenProtector tokens, IAppLinks links)
{
    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    /// <summary>Placeholders each template may use (shown to admins; unknown ones are rejected on save).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Placeholders = new Dictionary<string, string[]>
    {
        [TemplateCodes.BookingConfirmed] = ["student_name", "reference", "session_title", "session_time", "link"],
        [TemplateCodes.PaymentRejected] = ["student_name", "reference", "reason", "link"],
        [TemplateCodes.SessionReminder] = ["student_name", "reference", "session_title", "session_time", "link"],
        [TemplateCodes.TopUpRequired] = ["student_name", "reference", "session_title", "session_time", "amount", "link"],
        [TemplateCodes.RefundCompleted] = ["student_name", "reference", "amount"],
        [TemplateCodes.SessionCancelled] = ["student_name", "reference", "session_title", "session_time", "reason"],
        [TemplateCodes.RequestReceived] = ["student_name", "reference"],
        [TemplateCodes.SessionOffer] = ["student_name", "reference", "session_title", "session_time", "amount", "link"],
    };

    // ---------- Queueing (called inside the caller's unit of work; the caller saves) ----------

    public async Task QueueForBookingAsync(Guid bookingId, string code, string dedupKey, CancellationToken ct,
        IReadOnlyDictionary<string, string>? extra = null, DateTime? dueAtUtc = null)
    {
        if (await AlreadyQueuedAsync(dedupKey, ct)) return;
        var template = await ActiveTemplateAsync(code, ct);
        if (template is null) return;
        var b = await db.Bookings.AsNoTracking().Where(x => x.Id == bookingId)
            .Select(x => new
            {
                x.Reference, x.Language, x.Currency, Name = x.Student!.Name, Phone = x.Student.ContactPhoneE164, Guardian = x.Student.GuardianPhoneE164,
                x.Session!.ScopeTitleAr, x.Session.ScopeTitleEn, x.Session.StartUtc,
            }).FirstOrDefaultAsync(ct);
        if (b?.Phone is null) return; // no contact yet (hold without details)

        var en = b.Language == "en";
        var vars = new Dictionary<string, string>
        {
            ["student_name"] = b.Name,
            ["reference"] = b.Reference,
            ["session_title"] = en && !string.IsNullOrWhiteSpace(b.ScopeTitleEn) ? b.ScopeTitleEn : b.ScopeTitleAr,
            ["session_time"] = FormatTime(b.StartUtc, en),
        };
        foreach (var (k, v) in extra ?? new Dictionary<string, string>()) vars[k] = v;
        if (vars.TryGetValue("amount", out var amount) && decimal.TryParse(amount, CultureInfo.InvariantCulture, out var a))
            vars["amount"] = FormatMoney(a, b.Currency, en);

        Add(template, en, vars, dedupKey, b.Name, b.Phone, b.Guardian, bookingId, null, dueAtUtc);
    }

    public async Task QueueForRequestAsync(SessionRequest r, string code, CancellationToken ct)
    {
        var dedupKey = $"{code}:{r.Id}";
        if (await AlreadyQueuedAsync(dedupKey, ct)) return;
        var template = await ActiveTemplateAsync(code, ct);
        if (template is null) return;
        var en = r.Language == "en";
        Add(template, en, new Dictionary<string, string> { ["student_name"] = r.StudentName, ["reference"] = r.Reference },
            dedupKey, r.StudentName, r.ContactPhoneE164, r.GuardianPhoneE164, null, r.Id, null);
    }

    /// <summary>Background job: queue reminders for confirmed bookings in sessions starting within the next 3 hours.</summary>
    public async Task<int> QueueRemindersAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.Confirmed && b.Session!.StartUtc > now && b.Session.StartUtc <= now.AddHours(3)
                        && b.Session.Status != SessionStatus.Cancelled)
            .Select(b => new { b.Id, b.Session!.StartUtc }).ToListAsync(ct);
        foreach (var b in bookings)
            await QueueForBookingAsync(b.Id, TemplateCodes.SessionReminder, $"reminder:{b.Id}", ct, dueAtUtc: b.StartUtc.AddHours(-2));
        await db.SaveChangesAsync(ct);
        return bookings.Count;
    }

    // ---------- Admin: log ----------

    public async Task<IReadOnlyList<NotificationDto>> ListAsync(NotificationStatus? status, CancellationToken ct)
    {
        var q = db.Notifications.AsNoTracking();
        if (status is { } s) q = q.Where(n => n.Status == s);
        q = status == NotificationStatus.Pending ? q.OrderBy(n => n.DueAtUtc) : q.OrderByDescending(n => n.CreatedAtUtc);
        var titles = await db.MessageTemplates.AsNoTracking().ToDictionaryAsync(t => t.Code, t => t.TitleAr, ct);
        var rows = await (from n in q.Take(300)
                          join b in db.Bookings on n.BookingId equals b.Id into bj
                          from b in bj.DefaultIfEmpty()
                          select new { n, Reference = b == null ? null : b.Reference }).ToListAsync(ct);
        return rows.Select(x => new NotificationDto(x.n.Id, x.n.TemplateCode, titles.GetValueOrDefault(x.n.TemplateCode, x.n.TemplateCode),
            x.n.Status.ToString(), x.n.RecipientName, x.n.RecipientPhoneE164, x.n.GuardianPhoneE164, x.n.Language, x.n.Body,
            x.Reference, x.n.CreatedAtUtc, x.n.DueAtUtc, x.n.HandledAtUtc, x.n.HandledBy)).ToList();
    }

    public Task<int> PendingCountAsync(CancellationToken ct) =>
        db.Notifications.CountAsync(n => n.Status == NotificationStatus.Pending && n.DueAtUtc <= clock.UtcNow.AddHours(1), ct);

    /// <summary>Returns the final text (with the student's link) and a WhatsApp chat link with no text in the URL.</summary>
    public async Task<ComposedMessage> ComposeAsync(Guid id, CancellationToken ct)
    {
        var n = await db.Notifications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new DomainException(AdminErrors.NotFound, "الرسالة مش موجودة.", "Message not found.");
        var text = n.Body;
        if (text.Contains("{link}"))
        {
            var protectedToken = n.BookingId is null ? null
                : await db.Bookings.Where(b => b.Id == n.BookingId).Select(b => b.AccessTokenProtected).FirstOrDefaultAsync(ct);
            var token = protectedToken is null ? null : tokens.Unprotect(protectedToken);
            text = text.Replace("{link}", token is null
                ? (n.Language == "en" ? "(link unavailable — issue a new link from the session's bookings)" : "(اللينك مش متاح — اعمل لينك جديد من حجوزات الجلسة)")
                : links.BookingLink(token));
        }
        static string Chat(string phone) => $"https://wa.me/{new string(phone.Where(char.IsDigit).ToArray())}";
        return new ComposedMessage(text, Chat(n.RecipientPhoneE164), n.GuardianPhoneE164 is null ? null : Chat(n.GuardianPhoneE164));
    }

    public async Task MarkAsync(Guid id, bool sent, string actor, CancellationToken ct)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new DomainException(AdminErrors.NotFound, "الرسالة مش موجودة.", "Message not found.");
        n.Status = sent ? NotificationStatus.Sent : NotificationStatus.Skipped;
        n.HandledAtUtc = clock.UtcNow;
        n.HandledBy = actor;
        await db.SaveChangesAsync(ct);
    }

    // ---------- Admin: templates ----------

    public async Task<IReadOnlyList<TemplateDto>> TemplatesAsync(CancellationToken ct) =>
        (await db.MessageTemplates.AsNoTracking().OrderBy(t => t.Code).ToListAsync(ct))
            .Select(t => new TemplateDto(t.Code, t.TitleAr, t.TitleEn, t.BodyAr, t.BodyEn, t.IsActive,
                Placeholders.GetValueOrDefault(t.Code, []))).ToList();

    public async Task SaveTemplateAsync(string code, SaveTemplateRequest req, string actor, CancellationToken ct)
    {
        var t = await db.MessageTemplates.FirstOrDefaultAsync(x => x.Code == code, ct)
                ?? throw new DomainException(AdminErrors.NotFound, "القالب مش موجود.", "Template not found.");
        var allowed = Placeholders.GetValueOrDefault(code, []);
        foreach (var body in new[] { req.BodyAr, req.BodyEn })
        {
            if (string.IsNullOrWhiteSpace(body) || body.Length > 2000)
                throw new DomainException(AdminErrors.Invalid, "نص الرسالة لازم يكون بين 1 و2000 حرف.", "The message text must be 1–2000 characters.");
            var unknown = Regex.Matches(body, @"\{([a-z_]+)\}").Select(m => m.Groups[1].Value).Where(p => !allowed.Contains(p)).Distinct().ToList();
            if (unknown.Count > 0)
                throw new DomainException(AdminErrors.Invalid,
                    $"فيه متغيرات مش معروفة: {string.Join("، ", unknown.Select(u => "{" + u + "}"))}",
                    $"Unknown placeholders: {string.Join(", ", unknown.Select(u => "{" + u + "}"))}");
        }
        t.BodyAr = req.BodyAr.Trim();
        t.BodyEn = req.BodyEn.Trim();
        t.IsActive = req.IsActive;
        t.UpdatedBy = actor;
        t.UpdatedAtUtc = clock.UtcNow;
        db.Audit(clock, actor, nameof(MessageTemplate), code, "Update", req);
        await db.SaveChangesAsync(ct);
    }

    // ---------- helpers ----------

    private async Task<bool> AlreadyQueuedAsync(string dedupKey, CancellationToken ct) =>
        db.Notifications.Local.Any(n => n.DedupKey == dedupKey) || await db.Notifications.AnyAsync(n => n.DedupKey == dedupKey, ct);

    private Task<MessageTemplate?> ActiveTemplateAsync(string code, CancellationToken ct) =>
        db.MessageTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Code == code && t.IsActive, ct);

    private void Add(MessageTemplate t, bool en, Dictionary<string, string> vars, string dedupKey, string name, string phone,
        string? guardian, Guid? bookingId, Guid? requestId, DateTime? dueAtUtc)
    {
        var now = clock.UtcNow;
        var body = Regex.Replace(en ? t.BodyEn : t.BodyAr, @"\{([a-z_]+)\}",
            m => m.Groups[1].Value == "link" ? m.Value : vars.GetValueOrDefault(m.Groups[1].Value, ""));
        db.Notifications.Add(new Notification
        {
            TemplateCode = t.Code, BookingId = bookingId, SessionRequestId = requestId, RecipientName = name, RecipientPhoneE164 = phone,
            GuardianPhoneE164 = guardian, Language = en ? "en" : "ar", Body = body, DedupKey = dedupKey, CreatedAtUtc = now,
            DueAtUtc = dueAtUtc is { } d && d > now ? d : now,
        });
    }

    private static string FormatTime(DateTime utc, bool en)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Cairo);
        return en
            ? local.ToString("dddd d MMMM, h:mm tt", CultureInfo.GetCultureInfo("en-US"))
            : local.ToString("dddd d MMMM، h:mm tt", CultureInfo.GetCultureInfo("ar-EG"));
    }

    private static string FormatMoney(decimal amount, string currency, bool en) =>
        en ? $"{amount.ToString("0.##", CultureInfo.InvariantCulture)} {currency}"
           : $"{amount.ToString("0.##", CultureInfo.InvariantCulture)} {(currency == "EGP" ? "ج.م" : currency)}";
}

/// <summary>Default texts, inserted once (admins edit them afterwards).</summary>
public static class TemplateDefaults
{
    public static readonly MessageTemplate[] All =
    [
        T(TemplateCodes.BookingConfirmed, "تأكيد الحجز", "Booking confirmed",
            "أهلاً {student_name} 👋\nحجزك رقم {reference} في \"{session_title}\" يوم {session_time} اتأكد ✓\nمن اللينك ده تتابع حجزك وتدخل الجلسة في ميعادها (اللينك خاص بيك متبعتهوش لحد):\n{link}",
            "Hi {student_name} 👋\nYour booking {reference} for \"{session_title}\" on {session_time} is confirmed ✓\nUse this private link to follow your booking and join the session on time (don't share it):\n{link}"),
        T(TemplateCodes.PaymentRejected, "رفض إيصال الدفع", "Payment proof rejected",
            "أهلاً {student_name}، إيصال الدفع لحجز {reference} محتاج يتراجع: {reason}.\nارفع صورة تانية من صفحة حجزك:\n{link}",
            "Hi {student_name}, the payment proof for booking {reference} needs another look: {reason}.\nPlease upload a new screenshot from your booking page:\n{link}"),
        T(TemplateCodes.SessionReminder, "تذكير قبل الجلسة", "Session reminder",
            "تذكير ⏰ جلسة \"{session_title}\" بتبدأ {session_time}.\nادخل من صفحة حجزك في الميعاد:\n{link}",
            "Reminder ⏰ \"{session_title}\" starts {session_time}.\nJoin from your booking page on time:\n{link}"),
        T(TemplateCodes.TopUpRequired, "الجروب مكملش العدد", "Group did not fill",
            "أهلاً {student_name}، جروب \"{session_title}\" ({session_time}) مكملش العدد المطلوب.\nعلشان الجلسة تتم في ميعادها المطلوب فرق {amount}، أو تقدر تنقل لميعاد تاني أو تسترد فلوسك كاملة.\nاختار من صفحة حجزك:\n{link}",
            "Hi {student_name}, the group for \"{session_title}\" ({session_time}) didn't reach the needed size.\nTo keep the same time there's a difference of {amount}, or you can move to another time or get a full refund.\nChoose from your booking page:\n{link}"),
        T(TemplateCodes.RefundCompleted, "تم الاسترداد", "Refund sent",
            "أهلاً {student_name}، رجّعنالك {amount} لحجز {reference}. لو محتاج أي حاجة إحنا موجودين.",
            "Hi {student_name}, we've sent back {amount} for booking {reference}. Let us know if you need anything."),
        T(TemplateCodes.SessionCancelled, "إلغاء الجلسة", "Session cancelled",
            "أهلاً {student_name}، للأسف جلسة \"{session_title}\" يوم {session_time} اتلغت ({reason}).\nلو كنت دفعت هنرجّعلك فلوسك كاملة، وهنكلمك نرتّب ميعاد تاني لو حابب.",
            "Hi {student_name}, unfortunately \"{session_title}\" on {session_time} was cancelled ({reason}).\nIf you paid, you'll get a full refund, and we can arrange another time if you like."),
        T(TemplateCodes.RequestReceived, "استلام طلب جلسة", "Session request received",
            "أهلاً {student_name}، وصلنا طلبك رقم {reference} ✓ هنرتّب الجلسة ونكلمك قريب.",
            "Hi {student_name}, we received your request {reference} ✓ We'll arrange the session and get back to you soon."),
        T(TemplateCodes.SessionOffer, "ميعاد جاهز للطلب", "Time offered for a request",
            "أهلاً {student_name} 👋\nجهزنالك \"{session_title}\" يوم {session_time}.\nالمبلغ المطلوب {amount}. أكّد بياناتك وادفع من اللينك ده علشان نثبّت الميعاد:\n{link}",
            "Hi {student_name} 👋\nWe've arranged \"{session_title}\" on {session_time}.\nThe amount due is {amount}. Confirm your details and pay from this link to secure the time:\n{link}"),
    ];

    private static MessageTemplate T(string code, string titleAr, string titleEn, string ar, string en) =>
        new() { Code = code, TitleAr = titleAr, TitleEn = titleEn, BodyAr = ar, BodyEn = en, UpdatedAtUtc = DateTime.UtcNow };

    /// <summary>Inserts any missing default template (idempotent; never overwrites admin edits).</summary>
    public static async Task EnsureAsync(IDoseDbContext db, CancellationToken ct = default)
    {
        var existing = await db.MessageTemplates.Select(t => t.Code).ToListAsync(ct);
        foreach (var t in All.Where(t => !existing.Contains(t.Code)))
            db.MessageTemplates.Add(new MessageTemplate { Code = t.Code, TitleAr = t.TitleAr, TitleEn = t.TitleEn, BodyAr = t.BodyAr, BodyEn = t.BodyEn, UpdatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
    }
}
