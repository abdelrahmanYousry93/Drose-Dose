using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Dose.Application.Bookings;
using Dose.Domain.Administration;
using Dose.Domain.Sessions;
using Dose.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dose.Api;

/// <summary>
/// Instant alerts to the owner's Telegram: a student started a booking, entered their details, uploaded a payment
/// proof or asked for a session; a teacher offered free time or proposed a session.
/// Inactive until Telegram__BotToken is set (encrypted environment variable). The chat is the first person who sends
/// /start to the bot (remembered), or Telegram__ChatId when set.
/// </summary>
public sealed class TelegramSettings
{
    public const string SectionName = "Telegram";
    public string? BotToken { get; init; }
    public string? ChatId { get; init; }
    public bool Enabled => !string.IsNullOrWhiteSpace(BotToken);
}

/// <summary>Queue + sender. Alerts never block or fail the student's request: they are queued and sent in the background.</summary>
public sealed class OwnerAlertQueue(
    IOptions<TelegramSettings> options, IHttpClientFactory http, IServiceScopeFactory scopes, ILogger<OwnerAlertQueue> log) : BackgroundService
{
    readonly Channel<string> queue = Channel.CreateBounded<string>(new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropOldest });
    string? chatId;

    public bool Enabled => options.Value.Enabled;

    /// <summary>Last outcome, shown (without secrets) in /api/health so a missing alert can be diagnosed.</summary>
    public string Status { get; private set; } = "not started";
    public bool ChatLinked => chatId is not null || !string.IsNullOrWhiteSpace(options.Value.ChatId);
    void Mark(string status) => Status = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm}Z {status}";
    public void Enqueue(string text) { if (Enabled) queue.Writer.TryWrite(text); }
    public void Note(string status) => Mark(status);

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        if (!Enabled) { Status = "off: Telegram__BotToken not set"; return; }
        try
        {
            using var me = await http.CreateClient("telegram").GetAsync(Api("getMe"), stop);
            Mark(me.IsSuccessStatusCode ? "ready: bot token accepted" : $"bot token rejected by Telegram ({(int)me.StatusCode})");
            if (me.IsSuccessStatusCode) await ChatAsync(http.CreateClient("telegram"), stop);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { Mark("cannot reach Telegram: " + ex.GetType().Name); }
        await foreach (var text in queue.Reader.ReadAllAsync(stop))
        {
            try { await SendAsync(text, stop); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Mark("send failed: " + ex.GetType().Name);
                log.LogWarning("Owner alert not sent: {Error}", ex.Message);
            }
        }
    }

    string Api(string method) => $"https://api.telegram.org/bot{options.Value.BotToken}/{method}";

    async Task SendAsync(string text, CancellationToken ct)
    {
        var client = http.CreateClient("telegram");
        var chat = await ChatAsync(client, ct);
        if (chat is null)
        {
            Mark("skipped: nobody has pressed Start in the bot chat yet");
            log.LogWarning("Owner alert skipped: nobody has sent /start to the Telegram bot yet");
            return;
        }
        using var res = await client.PostAsJsonAsync(Api("sendMessage"),
            new { chat_id = chat, text, disable_web_page_preview = true }, ct);
        if (res.IsSuccessStatusCode) Mark("sent");
        else
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            Mark($"Telegram refused the message ({(int)res.StatusCode}): {body[..Math.Min(body.Length, 160)]}");
            log.LogWarning("Telegram answered {Status}", (int)res.StatusCode);
        }
    }

    /// <summary>Configured chat, else the remembered one, else the first private chat that messaged the bot.</summary>
    async Task<string?> ChatAsync(HttpClient client, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(options.Value.ChatId)) return options.Value.ChatId;
        if (chatId is not null) return chatId;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DoseDbContext>();
        chatId = await db.AuditLogs.Where(a => a.Entity == "TelegramChat").OrderBy(a => a.AtUtc).Select(a => a.EntityId).FirstOrDefaultAsync(ct);
        if (chatId is not null) return chatId;

        using var res = await client.GetAsync(Api("getUpdates"), ct);
        if (!res.IsSuccessStatusCode)
        {
            Mark($"getUpdates failed ({(int)res.StatusCode})");
            return null;
        }
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        foreach (var update in doc.RootElement.GetProperty("result").EnumerateArray())
            if (update.TryGetProperty("message", out var m) && m.TryGetProperty("chat", out var c)
                && c.TryGetProperty("type", out var type) && type.GetString() == "private")
            {
                chatId = c.GetProperty("id").GetRawText();
                db.AuditLogs.Add(new AuditLog { Actor = "telegram", Entity = "TelegramChat", EntityId = chatId, Action = "linked", AtUtc = DateTime.UtcNow });
                await db.SaveChangesAsync(ct);
                return chatId;
            }
        return null;
    }
}

/// <summary>Builds the alert texts (Arabic) from the database. Every method swallows its own errors.</summary>
public sealed class OwnerAlerts(DoseDbContext db, OwnerAlertQueue queue, IOptions<PublicSettings> site, ILogger<OwnerAlerts> log)
{
    static readonly TimeZoneInfo Cairo = FindCairo();
    static TimeZoneInfo FindCairo()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.CreateCustomTimeZone("Cairo", TimeSpan.FromHours(2), "Cairo", "Cairo"); }
    }
    static string When(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Cairo).ToString("ddd d/M — h:mm tt", new System.Globalization.CultureInfo("ar-EG"));
    static string Chat(string? phone) => string.IsNullOrWhiteSpace(phone) ? "—" : $"{phone}  (https://wa.me/{new string(phone.Where(char.IsDigit).ToArray())})";
    string Admin => $"{site.Value.AppBaseUrl.TrimEnd('/')}/admin";

    async Task Try(Func<Task<string?>> build)
    {
        if (!queue.Enabled) return;
        try { if (await build() is { } text) queue.Enqueue(text); }
        catch (Exception ex) { log.LogWarning(ex, "Owner alert not built"); queue.Note("alert not built: " + ex.GetType().Name + " " + ex.Message[..Math.Min(ex.Message.Length, 120)]); }
    }

    async Task<string> SessionLinesAsync(Guid sessionId)
    {
        var s = await db.Sessions.AsNoTracking().Where(x => x.Id == sessionId)
            .Select(x => new { x.ScopeTitleAr, Grade = x.Grade!.NameAr, Subject = x.Subject!.NameAr, Teacher = x.Teacher!.DisplayNameAr, x.StartUtc, x.Mode, x.IsClosed })
            .SingleAsync();
        var kind = s.Mode == SessionMode.Private ? "خاصة" : s.IsClosed ? "مجموعة صحاب" : "جروب مفتوح";
        return $"📘 {s.ScopeTitleAr}\n{s.Subject} — {s.Grade}\n🕒 {When(s.StartUtc)} (مصر) — {kind}\n👨‍🏫 {s.Teacher}";
    }

    async Task<string?> BookingTextAsync(System.Linq.Expressions.Expression<Func<Dose.Domain.Bookings.Booking, bool>> which, string title, bool withContact)
    {
        var b = await db.Bookings.AsNoTracking().Where(which)
            .Select(x => new { x.Reference, x.SessionId, x.QuotedPrice, x.SeatCount, Name = x.Student!.Name, Phone = x.Student.ContactPhoneE164,
                Guardian = x.Student.GuardianName, GuardianPhone = x.Student.GuardianPhoneE164 })
            .FirstOrDefaultAsync();
        if (b is null) return null;
        if (b.Name is null) title += " (لسه ماكتبش بياناته)";
        var text = $"{title}\n\n{await SessionLinesAsync(b.SessionId)}\n💵 {b.QuotedPrice:0.##} ج.م" + (b.SeatCount > 1 ? $" ({b.SeatCount} طلاب)" : "") + $"\n🔖 {b.Reference}";
        if (withContact && b.Name is not null)
        {
            text += $"\n\n👤 {b.Name}\n📱 {Chat(b.Phone)}";
            if (!string.IsNullOrWhiteSpace(b.Guardian) || !string.IsNullOrWhiteSpace(b.GuardianPhone)) text += $"\nولي الأمر: {b.Guardian} {Chat(b.GuardianPhone)}";
        }
        return text + $"\n\n{Admin}";
    }

    public Task HoldStartedAsync(string reference) => Try(() =>
        BookingTextAsync(b => b.Reference == reference, "🟡 حد بدأ يحجز", withContact: true));

    public Task DetailsSubmittedAsync(string token) => Try(() =>
    {
        var hash = AccessTokens.Hash(token);
        return BookingTextAsync(b => b.AccessTokenHash == hash, "🟢 طالب كتب بياناته — مستني الدفع", withContact: true);
    });

    public Task ProofUploadedAsync(string token) => Try(() =>
    {
        var hash = AccessTokens.Hash(token);
        return BookingTextAsync(b => b.AccessTokenHash == hash, "💰 إثبات دفع اترفع — راجعه ووافق من لوحة الإدارة", withContact: true);
    });

    public Task RequestCreatedAsync(string reference) => Try(async () =>
    {
        var r = await db.SessionRequests.AsNoTracking().Where(x => x.Reference == reference)
            .Select(x => new { x.Reference, x.StudentName, x.ContactPhoneE164, x.ScopeText, x.PreferredDays, x.PreferredTimes, x.Mode, x.GroupSize, x.Notes, x.GradeId, x.SubjectId })
            .FirstOrDefaultAsync();
        if (r is null) return null;
        var grade = await db.Grades.Where(g => g.Id == r.GradeId).Select(g => g.NameAr).FirstOrDefaultAsync();
        var subject = await db.Subjects.Where(s => s.Id == r.SubjectId).Select(s => s.NameAr).FirstOrDefaultAsync();
        return $"📩 طلب جلسة جديدة\n\n📘 {r.ScopeText}\n{subject} — {grade}\n🕒 {r.PreferredDays} / {r.PreferredTimes}\n"
             + (r.Mode == SessionMode.Private ? "خاصة" : $"جروب{(r.GroupSize is { } n ? $" ({n})" : "")}")
             + (string.IsNullOrWhiteSpace(r.Notes) ? "" : $"\n📝 {r.Notes}")
             + $"\n🔖 {r.Reference}\n\n👤 {r.StudentName}\n📱 {Chat(r.ContactPhoneE164)}\n\n{Admin}";
    });

    public Task AvailabilitySubmittedAsync(Guid id) => Try(async () =>
    {
        var a = await db.TeacherAvailabilities.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { Teacher = x.Teacher!.DisplayNameAr, x.StartUtc, x.EndUtc, x.Note }).FirstOrDefaultAsync();
        if (a is null) return null;
        return $"👨‍🏫 مدرس ضاف ميعاد جديد — محتاج موافقتك\n\n{a.Teacher}\n🕒 {When(a.StartUtc)} ← {When(a.EndUtc)} (مصر)"
             + (string.IsNullOrWhiteSpace(a.Note) ? "" : $"\n📝 {a.Note}") + $"\n\n{Admin}";
    });

    public Task SessionProposedAsync(Guid sessionId) => Try(async () =>
        $"👨‍🏫 مدرس اقترح جلسة جديدة — محتاجة موافقتك\n\n{await SessionLinesAsync(sessionId)}\n\n{Admin}");
}
