using Dose.Application.Abstractions;
using Dose.Application.Administration;
using Dose.Contracts;
using Dose.Contracts.Admin;
using Dose.Domain.Bookings;
using Dose.Domain.Common;
using Dose.Domain.Sessions;
using Dose.Domain.Teachers;
using Microsoft.EntityFrameworkCore;

namespace Dose.Application.Teachers;

public static class TeacherErrors
{
    public const string NotLinked = "teacher.not_linked";
    public const string NotYours = "teacher.not_yours";
    public const string Invalid = "teacher.invalid";
}

/// <summary>
/// Teacher area (spec 3, 9.9): own sessions with meeting link, student first names only (no contact details),
/// attendance, completion, and availability submissions for admin approval.
/// </summary>
public sealed class TeacherAreaService(IDoseDbContext db, IClock clock)
{
    /// <summary>Attendance can be recorded from shortly before the start until this long after the end.</summary>
    public static readonly TimeSpan AttendanceWindowAfterEnd = TimeSpan.FromHours(48);
    public static readonly TimeSpan AttendanceOpensBeforeStart = TimeSpan.FromMinutes(15);

    public async Task<Teacher> TeacherForUserAsync(string userId, CancellationToken ct) =>
        await db.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.UserId == userId, ct)
        ?? throw new DomainException(TeacherErrors.NotLinked, "حسابك مش مربوط بملف مدرس. كلّم الإدارة.", "Your account isn't linked to a teacher profile. Please contact the admin.");

    public async Task<IReadOnlyList<TeacherSessionDto>> SessionsAsync(string userId, bool past, CancellationToken ct)
    {
        var teacher = await TeacherForUserAsync(userId, ct);
        var now = clock.UtcNow;
        var q = db.Sessions.AsNoTracking().Where(s => s.TeacherId == teacher.Id && s.Status != SessionStatus.Draft);
        q = past ? q.Where(s => s.EndUtc < now.AddHours(-AttendanceWindowAfterEnd.TotalHours)).OrderByDescending(s => s.StartUtc)
                 : q.Where(s => s.EndUtc >= now.AddHours(-AttendanceWindowAfterEnd.TotalHours)).OrderBy(s => s.StartUtc);
        var rows = await Project(q.Take(100)).ToListAsync(ct);
        return rows.Select(r => ToDto(r, now)).ToList();
    }

    public async Task<TeacherSessionDetailsDto> DetailsAsync(string userId, Guid sessionId, CancellationToken ct)
    {
        var teacher = await TeacherForUserAsync(userId, ct);
        var row = await Project(db.Sessions.AsNoTracking().Where(s => s.Id == sessionId)).FirstOrDefaultAsync(ct)
                  ?? throw new DomainException(TeacherErrors.NotYours, "الجلسة مش موجودة.", "Session not found.");
        if (row.TeacherId != teacher.Id) throw new DomainException(TeacherErrors.NotYours, "الجلسة دي مش من جلساتك.", "This session isn't one of yours.");

        var students = await (from b in db.Bookings.AsNoTracking()
                              where b.SessionId == sessionId && (b.Status == BookingStatus.Confirmed
                                    || b.Status == BookingStatus.Completed || b.Status == BookingStatus.NoShow)
                              join a in db.Attendances on b.Id equals a.BookingId into att
                              from a in att.DefaultIfEmpty()
                              orderby b.CreatedAtUtc
                              select new { b.Id, Name = b.Student!.Name, Status = a == null ? (AttendanceStatus?)null : a.Status })
            .ToListAsync(ct);
        return new TeacherSessionDetailsDto(ToDto(row, clock.UtcNow),
            students.Select(s => new TeacherStudentDto(s.Id, FirstName(s.Name), s.Status?.ToString())).ToList());
    }

    public async Task MarkAttendanceAsync(string userId, Guid sessionId, MarkAttendanceRequest req, CancellationToken ct)
    {
        var teacher = await TeacherForUserAsync(userId, ct);
        var session = await OwnSessionAsync(teacher, sessionId, ct);
        var now = clock.UtcNow;
        if (!CanMark(session.Status, session.StartUtc, session.EndUtc, now))
            throw new DomainException(TeacherErrors.Invalid, "تسجيل الحضور متاح من قبل الجلسة بربع ساعة لحد يومين بعدها.", "Attendance can be recorded from 15 minutes before the session until 2 days after it.");

        var ids = req.Items.Select(i => i.BookingId).ToList();
        var valid = await db.Bookings.Where(b => b.SessionId == sessionId && ids.Contains(b.Id)
                                                 && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed || b.Status == BookingStatus.NoShow))
            .Select(b => b.Id).ToListAsync(ct);
        if (valid.Count != ids.Distinct().Count()) throw new DomainException(TeacherErrors.Invalid, "فيه طالب مش في الجلسة دي.", "A student is not part of this session.");

        var existing = await db.Attendances.Where(a => ids.Contains(a.BookingId)).ToListAsync(ct);
        foreach (var item in req.Items)
        {
            var status = item.Attended ? AttendanceStatus.Attended : AttendanceStatus.Absent;
            var row = existing.FirstOrDefault(a => a.BookingId == item.BookingId);
            if (row is null) db.Attendances.Add(new Attendance { BookingId = item.BookingId, Status = status, MarkedBy = teacher.DisplayNameAr, MarkedAtUtc = now });
            else { row.Status = status; row.MarkedBy = teacher.DisplayNameAr; row.MarkedAtUtc = now; }
        }
        db.Audit(clock, $"teacher:{teacher.Id}", nameof(Attendance), sessionId, "Mark", req.Items);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Marks the session completed: attended bookings → Completed, absent → NoShow. Everyone must be marked first.</summary>
    public async Task CompleteAsync(string userId, Guid sessionId, CancellationToken ct)
    {
        var teacher = await TeacherForUserAsync(userId, ct);
        var session = await OwnSessionAsync(teacher, sessionId, ct);
        var now = clock.UtcNow;
        if (session.Status is SessionStatus.Cancelled or SessionStatus.Completed)
            throw new DomainException(TeacherErrors.Invalid, "الجلسة ملغية أو متقفلة بالفعل.", "The session is already cancelled or closed.");
        if (now < session.StartUtc) throw new DomainException(TeacherErrors.Invalid, "الجلسة لسه مبدأتش.", "The session hasn't started yet.");

        var bookings = await db.Bookings.Where(b => b.SessionId == sessionId && b.Status == BookingStatus.Confirmed).ToListAsync(ct);
        var ids = bookings.Select(b => b.Id).ToList();
        var attendance = await db.Attendances.Where(a => ids.Contains(a.BookingId)).ToDictionaryAsync(a => a.BookingId, a => a.Status, ct);
        if (attendance.Count != bookings.Count)
            throw new DomainException(TeacherErrors.Invalid, "سجّل حضور كل الطلاب الأول.", "Record attendance for every student first.");

        foreach (var b in bookings)
            b.Status = attendance[b.Id] == AttendanceStatus.Attended ? BookingStatus.Completed : BookingStatus.NoShow;
        session.Status = SessionStatus.Completed;
        db.Audit(clock, $"teacher:{teacher.Id}", nameof(Session), sessionId, "Complete",
            new { attended = attendance.Count(a => a.Value == AttendanceStatus.Attended), absent = attendance.Count(a => a.Value == AttendanceStatus.Absent) });
        await db.SaveChangesAsync(ct);
    }

    // ---------- Availability ----------

    public async Task<IReadOnlyList<AvailabilityDto>> AvailabilityAsync(string userId, CancellationToken ct)
    {
        var teacher = await TeacherForUserAsync(userId, ct);
        var from = clock.UtcNow.AddDays(-7);
        return await db.TeacherAvailabilities.AsNoTracking().Where(a => a.TeacherId == teacher.Id && a.EndUtc >= from)
            .OrderBy(a => a.StartUtc)
            .Select(a => new AvailabilityDto(a.Id, a.StartUtc, a.EndUtc, a.Note, a.Status.ToString(), a.ReviewNote)).ToListAsync(ct);
    }

    public async Task<Guid> SubmitAvailabilityAsync(string userId, SubmitAvailabilityRequest req, CancellationToken ct)
    {
        var teacher = await TeacherForUserAsync(userId, ct);
        var start = DateTime.SpecifyKind(req.StartUtc, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(req.EndUtc, DateTimeKind.Utc);
        var now = clock.UtcNow;
        if (start <= now) throw new DomainException(TeacherErrors.Invalid, "الميعاد لازم يكون في المستقبل.", "The time must be in the future.");
        if (end <= start) throw new DomainException(TeacherErrors.Invalid, "وقت النهاية لازم يكون بعد البداية.", "The end time must be after the start time.");
        if (end - start < TimeSpan.FromMinutes(30) || end - start > TimeSpan.FromHours(12))
            throw new DomainException(TeacherErrors.Invalid, "الميعاد لازم يكون بين نص ساعة و12 ساعة.", "A slot must be between 30 minutes and 12 hours.");
        if (start > now.AddDays(90)) throw new DomainException(TeacherErrors.Invalid, "تقدر تضيف مواعيد لحد 3 شهور قدام.", "You can add times up to 3 months ahead.");
        var overlaps = await db.TeacherAvailabilities.AnyAsync(a => a.TeacherId == teacher.Id
            && (a.Status == AvailabilityStatus.Submitted || a.Status == AvailabilityStatus.Approved)
            && a.StartUtc < end && start < a.EndUtc, ct);
        if (overlaps) throw new DomainException(TeacherErrors.Invalid, "الميعاد ده متداخل مع ميعاد تاني ليك.", "This slot overlaps another of your slots.");

        var slot = new TeacherAvailability
        {
            TeacherId = teacher.Id, StartUtc = start, EndUtc = end, Note = AdminGuard.Optional(req.Note, 300), CreatedAtUtc = now,
        };
        db.TeacherAvailabilities.Add(slot);
        await db.SaveChangesAsync(ct);
        return slot.Id;
    }

    public async Task WithdrawAvailabilityAsync(string userId, Guid id, CancellationToken ct)
    {
        var teacher = await TeacherForUserAsync(userId, ct);
        var slot = await db.TeacherAvailabilities.FirstOrDefaultAsync(a => a.Id == id && a.TeacherId == teacher.Id, ct)
                   ?? throw new DomainException(TeacherErrors.NotYours, "الميعاد مش موجود.", "Time slot not found.");
        if (slot.Status is AvailabilityStatus.Withdrawn or AvailabilityStatus.Rejected || slot.StartUtc <= clock.UtcNow)
            throw new DomainException(TeacherErrors.Invalid, "مينفعش تسحب الميعاد ده.", "This slot can't be withdrawn.");
        slot.Status = AvailabilityStatus.Withdrawn;
        await db.SaveChangesAsync(ct);
    }

    // ---------- Admin review ----------

    public async Task<IReadOnlyList<AvailabilityAdminDto>> AdminListAvailabilityAsync(string? status, CancellationToken ct)
    {
        var q = db.TeacherAvailabilities.AsNoTracking().Where(a => a.EndUtc >= clock.UtcNow.AddDays(-7));
        if (Enum.TryParse<AvailabilityStatus>(status, out var st)) q = q.Where(a => a.Status == st);
        return await q.OrderBy(a => a.StartUtc).Take(300)
            .Select(a => new AvailabilityAdminDto(a.Id, a.TeacherId, a.Teacher!.DisplayNameAr, a.StartUtc, a.EndUtc, a.Note,
                a.Status.ToString(), a.ReviewNote, a.CreatedAtUtc)).ToListAsync(ct);
    }

    public async Task ReviewAvailabilityAsync(Guid id, ReviewAvailabilityRequest req, string actor, CancellationToken ct)
    {
        var slot = await db.TeacherAvailabilities.FirstOrDefaultAsync(a => a.Id == id, ct)
                   ?? throw new DomainException(AdminErrors.NotFound, "الميعاد مش موجود.", "Time slot not found.");
        if (slot.Status != AvailabilityStatus.Submitted) throw new DomainException(AdminErrors.Invalid, "الميعاد ده اتراجع قبل كده.", "This slot was already reviewed.");
        slot.Status = req.Approve ? AvailabilityStatus.Approved : AvailabilityStatus.Rejected;
        slot.ReviewedBy = actor;
        slot.ReviewedAtUtc = clock.UtcNow;
        slot.ReviewNote = AdminGuard.Optional(req.Note, 300);
        db.Audit(clock, actor, nameof(TeacherAvailability), id, req.Approve ? "Approve" : "Reject", req);
        await db.SaveChangesAsync(ct);
    }

    // ---------- helpers ----------

    private sealed record Row(Guid Id, Guid TeacherId, string Title, string? Details, string Subject, string Grade, string Track,
        DateTime StartUtc, DateTime EndUtc, SessionMode Mode, SessionStatus Status, int Confirmed, int Capacity, string? MeetingUrl, int Marked);

    private IQueryable<Row> Project(IQueryable<Session> q) => q.Select(s => new Row(
        s.Id, s.TeacherId, s.ScopeTitleAr, s.ScopeDetailsAr, s.Subject!.NameAr, s.Grade!.NameAr, s.StudyTrack!.NameAr,
        s.StartUtc, s.EndUtc, s.Mode, s.Status,
        db.Bookings.Count(b => b.SessionId == s.Id && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed
                                                       || b.Status == BookingStatus.NoShow)),
        s.Capacity, s.MeetingUrl,
        db.Attendances.Count(a => db.Bookings.Any(b => b.Id == a.BookingId && b.SessionId == s.Id))));

    private static TeacherSessionDto ToDto(Row r, DateTime now) => new(
        r.Id, r.Title, r.Details, r.Subject, r.Grade, r.Track, r.StartUtc, r.EndUtc, r.Mode.ToString(), r.Status.ToString(),
        r.Confirmed, r.Capacity, r.Status == SessionStatus.Cancelled ? null : r.MeetingUrl,
        CanMark(r.Status, r.StartUtc, r.EndUtc, now), r.Marked);

    private static bool CanMark(SessionStatus status, DateTime start, DateTime end, DateTime now) =>
        status != SessionStatus.Cancelled && now >= start - AttendanceOpensBeforeStart && now <= end + AttendanceWindowAfterEnd;

    private async Task<Session> OwnSessionAsync(Teacher teacher, Guid sessionId, CancellationToken ct)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
                      ?? throw new DomainException(TeacherErrors.NotYours, "الجلسة مش موجودة.", "Session not found.");
        if (session.TeacherId != teacher.Id) throw new DomainException(TeacherErrors.NotYours, "الجلسة دي مش من جلساتك.", "This session isn't one of yours.");
        return session;
    }

    private static string FirstName(string? name) => (name ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "طالب";
}
