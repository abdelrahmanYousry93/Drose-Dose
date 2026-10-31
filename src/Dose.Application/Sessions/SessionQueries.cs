using System.Linq.Expressions;
using Dose.Application.Abstractions;
using Dose.Contracts;
using Dose.Domain.Bookings;
using Dose.Domain.Common;
using Dose.Domain.Pricing;
using Dose.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Dose.Application.Sessions;

public static class SeatRules
{
    /// <summary>Bookings that currently hold a seat (expired holds/unpaid bookings excluded).</summary>
    public static Expression<Func<Booking, bool>> OccupiesSeat(DateTime nowUtc) => b =>
        b.Status == BookingStatus.Confirmed
        || b.Status == BookingStatus.TopUpPending
        || b.Status == BookingStatus.PaymentUnderReview
        || (b.Status == BookingStatus.PendingHold && b.HoldExpiresAtUtc > nowUtc)
        || (b.Status == BookingStatus.PendingPayment && b.PaymentDueAtUtc > nowUtc);
}

public static class SessionPricing
{
    /// <summary>Per-student price a new booking pays now. Requires version tiers and target tier loaded.</summary>
    public static decimal CurrentPrice(Session s)
    {
        var version = s.PricingPlanVersion ?? throw new InvalidOperationException("Pricing version not loaded.");
        if (s.Mode == SessionMode.Private)
            return version.PrivatePrice
                   ?? throw new DomainException(PricingErrors.NotConfigured, "Private price is not configured for this session's pricing plan.");
        var tier = s.TargetTier ?? throw new DomainException(PricingErrors.TierNotFound, "Group session has no target tier.");
        return tier.PricePerStudent;
    }

    public static GroupTermsDto? GroupTerms(Session s)
    {
        if (s.Mode != SessionMode.Group || s.TargetTier is null || s.PricingPlanVersion is null) return null;
        var worst = GroupPricing.WorstCasePrice(s.PricingPlanVersion, s.TargetTier);
        return new GroupTermsDto(
            s.TargetTier.NameAr, s.TargetTier.MinParticipants, s.TargetTier.MaxParticipants,
            worst, worst - s.TargetTier.PricePerStudent, s.TierDecisionAtUtc);
    }
}

public sealed record SessionFilter(
    Guid? GradeId, Guid? SubjectId, Guid? StudyTrackId, Guid? ServiceTypeId, SessionMode? Mode, int Page = 1, int PageSize = 20);

public sealed class SessionQueries(IDoseDbContext db, IClock clock)
{
    private IQueryable<Session> WithDetails() => db.Sessions.AsNoTracking()
        .Include(s => s.Teacher).Include(s => s.Subject).Include(s => s.Grade!).ThenInclude(g => g.Stage)
        .Include(s => s.StudyTrack).Include(s => s.ServiceType).Include(s => s.TargetTier)
        .Include(s => s.PricingPlanVersion!).ThenInclude(v => v.Plan)
        .Include(s => s.PricingPlanVersion!).ThenInclude(v => v.Tiers);

    public async Task<IReadOnlyList<SessionSummaryDto>> ListAsync(SessionFilter f, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var q = WithDetails().Where(s => s.Status == SessionStatus.Published && s.BookingDeadlineUtc > now && s.StartUtc > now);
        if (f.GradeId is { } g) q = q.Where(s => s.GradeId == g);
        if (f.SubjectId is { } sub) q = q.Where(s => s.SubjectId == sub);
        if (f.StudyTrackId is { } t) q = q.Where(s => s.StudyTrackId == t);
        if (f.ServiceTypeId is { } st) q = q.Where(s => s.ServiceTypeId == st);
        if (f.Mode is { } m) q = q.Where(s => s.Mode == m);

        var pageSize = Math.Clamp(f.PageSize, 1, 50);
        var sessions = await q.OrderBy(s => s.StartUtc).Skip((Math.Max(1, f.Page) - 1) * pageSize).Take(pageSize)
            .AsSplitQuery().ToListAsync(ct);
        var counts = await CountsAsync(sessions.Select(s => s.Id).ToList(), ct);
        return sessions.Select(s => ToSummary(s, counts.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task<SessionDetailsDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var s = await WithDetails().AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id && x.Status != SessionStatus.Draft, ct);
        if (s is null) return null;
        var counts = await CountsAsync([s.Id], ct);
        return ToDetails(s, counts.GetValueOrDefault(s.Id), clock.UtcNow);
    }

    internal async Task<Dictionary<Guid, (int Confirmed, int Occupied)>> CountsAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var occupied = await db.Bookings.Where(b => ids.Contains(b.SessionId)).Where(SeatRules.OccupiesSeat(now))
            .GroupBy(b => b.SessionId)
            .Select(g => new
            {
                g.Key,
                Occupied = g.Count(),
                Confirmed = g.Count(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.TopUpPending),
            })
            .ToListAsync(ct);
        return occupied.ToDictionary(x => x.Key, x => (x.Confirmed, x.Occupied));
    }

    internal static SessionSummaryDto ToSummary(Session s, (int Confirmed, int Occupied) c) => new(
        s.Id, s.ScopeTitleAr, s.Subject!.NameAr, s.Grade!.NameAr, s.StudyTrack!.NameAr, s.ServiceType!.NameAr,
        s.Mode.ToString(), s.StartUtc, s.EndUtc, (int)(s.EndUtc - s.StartUtc).TotalMinutes,
        s.Teacher!.DisplayNameAr, s.Teacher.PhotoUrl,
        SessionPricing.CurrentPrice(s), s.PricingPlanVersion!.Plan!.Currency,
        s.Capacity, c.Confirmed, Math.Max(0, s.Capacity - c.Occupied), s.BookingDeadlineUtc);

    internal static TeacherPublicDto ToTeacher(Session s) => new(
        s.Teacher!.Id, s.Teacher.DisplayNameAr, s.Teacher.PhotoUrl, s.Teacher.BioAr, s.Teacher.QualificationsAr, s.Teacher.YearsOfExperience);

    internal static SessionDetailsDto ToDetails(Session s, (int Confirmed, int Occupied) c, DateTime now) => new(
        ToSummary(s, c), s.ScopeDetailsAr, s.MinParticipants, ToTeacher(s), SessionPricing.GroupTerms(s),
        s.Grade!.Stage!.RequiresGuardianContact,
        s.IsOpenForBooking(now) && c.Occupied < s.Capacity);
}
