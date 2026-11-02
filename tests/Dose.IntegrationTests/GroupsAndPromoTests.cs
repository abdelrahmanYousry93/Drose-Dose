using Dose.Application.Bookings;
using Dose.Contracts;
using Dose.Contracts.Admin;
using Dose.Domain.Bookings;
using Dose.Domain.Common;
using Dose.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Dose.IntegrationTests;

[Collection(DbCollection.Name)]
public sealed class GroupsAndPromoTests(DatabaseFixture fx)
{
    private const string Admin = "admin@test";
    private static int _phone = 700;

    private static void RequireDb() =>
        Assert.SkipWhen(DatabaseFixture.ServerConnection is null, "Set DOSE_TEST_SQL to run SQL Server integration tests.");

    private static string NextPhone() => $"0115{Interlocked.Increment(ref _phone):0000000}";

    private async Task<Guid> PromoAsync(string code, string kind, decimal value, int? perPhone = null, int? total = null, Guid? sessionId = null)
    {
        await using var db = fx.NewDb();
        return await fx.Promos(db, new FakeClock(fx.SeedTime)).SaveAsync(new PromoCodeAdminDto(null, code, "اختبار", kind, value,
            null, null, null, null, sessionId, null, null, total, perPhone, null, true), Admin, default);
    }

    [Fact]
    public async Task Percent_promo_lowers_the_price_respects_per_number_limit_and_also_lowers_a_group_top_up()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group, "جروب متوسط (5–9)");
        await PromoAsync("TEST20", "Percent", 20, perPhone: 1, sessionId: session.Id);
        var clock = new FakeClock(fx.SeedTime);
        var phone = NextPhone();

        string token; Guid bookingId; decimal list;
        await using (var db = fx.NewDb())
        {
            var svc = fx.Bookings(db, clock);
            var hold = await svc.CreateHoldAsync(session.Id, default);
            (token, bookingId, list) = (hold.AccessToken, hold.BookingId, hold.Price);
            var preview = await svc.PreviewPromoAsync(token, new PromoPreviewRequest(" test20 ", phone), default);
            Assert.Equal(list * 0.8m, preview.NetPrice);

            // Old price sent (promo ignored by the client) → rejected, nothing is charged by surprise.
            await Assert.ThrowsAsync<DomainException>(() => svc.SubmitDetailsAsync(token,
                new SubmitDetailsRequest("منى", phone, null, null, true, true, list, "TEST20"), default));
            await svc.SubmitDetailsAsync(token, new SubmitDetailsRequest("منى", phone, null, null, true, true, preview.NetPrice, "TEST20"), default);
            var view = await svc.GetViewAsync(token, default);
            Assert.Equal(preview.NetPrice, view.Price);
            Assert.Equal(list - preview.NetPrice, view.DiscountAmount);
            Assert.Equal("TEST20", view.PromoCode);
        }

        // Same number again → per-number limit.
        await using (var db = fx.NewDb())
        {
            var svc = fx.Bookings(db, clock);
            var hold2 = await svc.CreateHoldAsync(session.Id, default);
            var ex = await Assert.ThrowsAsync<DomainException>(() => svc.PreviewPromoAsync(hold2.AccessToken, new PromoPreviewRequest("TEST20", phone), default));
            Assert.Equal(PromoErrors.Invalid, ex.Code);
        }

        // Pay, then the group closes alone: the difference keeps the 20% off.
        await using (var db = fx.NewDb())
        {
            using var png = DatabaseFixture.TinyPng();
            await fx.Bookings(db, clock).SubmitPaymentProofAsync(token, await DatabaseFixture.PaymentMethodIdAsync(db), png, png.Length, null, default);
            var proofId = await db.PaymentProofs.Where(p => p.BookingId == bookingId).Select(p => p.Id).SingleAsync();
            await fx.Payments(db, clock).ApproveAsync(proofId, Admin, default);
        }
        clock.UtcNow = session.TierDecisionAtUtc!.Value.AddMinutes(1);
        await using (var db = fx.NewDb()) await fx.Decisions(db, clock).RunDueDecisionsAsync(default);
        await using (var db = fx.NewDb())
        {
            var adj = await db.FinancialAdjustments.SingleAsync(a => a.BookingId == bookingId && a.Type == AdjustmentType.TopUpDue && a.Status == AdjustmentStatus.Pending);
            Assert.Equal(40m, adj.Amount); // alone → private 150; (150 − 100) × 80%
        }
    }

    [Fact]
    public async Task Promo_total_limit_and_scope_are_enforced()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group);
        var other = await fx.CreateSessionAsync(SessionMode.Group);
        await PromoAsync("ONCE50", "Fixed", 50, total: 1, sessionId: session.Id);
        var clock = new FakeClock(fx.SeedTime);
        await using var db = fx.NewDb();
        var svc = fx.Bookings(db, clock);

        var elsewhere = await svc.CreateHoldAsync(other.Id, default);
        Assert.Equal(PromoErrors.Invalid, (await Assert.ThrowsAsync<DomainException>(() =>
            svc.PreviewPromoAsync(elsewhere.AccessToken, new PromoPreviewRequest("ONCE50", null), default))).Code);

        var first = await svc.CreateHoldAsync(session.Id, default);
        var p = await svc.PreviewPromoAsync(first.AccessToken, new PromoPreviewRequest("ONCE50", null), default);
        Assert.Equal(first.Price - 50, p.NetPrice);
        await svc.SubmitDetailsAsync(first.AccessToken, new SubmitDetailsRequest("علي", NextPhone(), null, null, true, true, p.NetPrice, "ONCE50"), default);

        var second = await svc.CreateHoldAsync(session.Id, default);
        await Assert.ThrowsAsync<DomainException>(() => svc.PreviewPromoAsync(second.AccessToken, new PromoPreviewRequest("ONCE50", null), default));
    }

    [Fact]
    public async Task Private_group_organizer_pays_for_all_seats_then_friends_join_with_the_invite_code()
    {
        RequireDb();
        var clock = new FakeClock(fx.SeedTime);
        Guid gradeId, subjectId, trackId, serviceId;
        await using (var db = fx.NewDb())
        {
            var gs = await (from x in db.GradeSubjects join g in db.Grades on x.GradeId equals g.Id where g.Stage!.NameAr == "ثانوي" select x).FirstAsync();
            (gradeId, subjectId) = (gs.GradeId, gs.SubjectId);
            trackId = (await db.StudyTracks.FirstAsync()).Id;
            serviceId = (await db.ServiceTypes.FirstAsync(s => s.Code == "one_lesson")).Id;
        }

        SlotsDto slots;
        await using (var db = fx.NewDb()) slots = await fx.Private(db, clock).SlotsAsync(gradeId, subjectId, trackId, serviceId, 3, default);
        Assert.NotEmpty(slots.Slots);
        Assert.Equal(slots.PricePerStudent * 3, slots.Total);
        Assert.All(slots.Slots, s => Assert.True(s.StartUtc >= fx.SeedTime.AddHours(24)));
        var slot = slots.Slots[0];

        CreateHoldResponse hold;
        await using (var db = fx.NewDb())
            hold = await fx.Private(db, clock).CreateAsync(new CreatePrivateBookingRequest(gradeId, subjectId, trackId, serviceId, 3, slot.TeacherId, slot.StartUtc, "الدرس الأول"), default);
        Assert.Equal(slots.Total, hold.Price);

        // The same teacher time can't be taken twice, and it no longer appears as free.
        await using (var db = fx.NewDb())
        {
            await Assert.ThrowsAsync<DomainException>(() => fx.Private(db, clock).CreateAsync(
                new CreatePrivateBookingRequest(gradeId, subjectId, trackId, serviceId, 2, slot.TeacherId, slot.StartUtc, null), default));
            var again = await fx.Private(db, clock).SlotsAsync(gradeId, subjectId, trackId, serviceId, 2, default);
            Assert.DoesNotContain(again.Slots, s => s.TeacherId == slot.TeacherId && s.StartUtc == slot.StartUtc);
        }

        // Hidden from the public list; normal holds are refused.
        await using (var db = fx.NewDb())
        {
            var sessionId = await db.Bookings.Where(b => b.Id == hold.BookingId).Select(b => b.SessionId).SingleAsync();
            Assert.DoesNotContain(await new Dose.Application.Sessions.SessionQueries(db, clock).ListAsync(new(null, null, null, null, null), default), s => s.Id == sessionId);
            await Assert.ThrowsAsync<DomainException>(() => fx.Bookings(db, clock).CreateHoldAsync(sessionId, default));
        }

        // Organizer: details (no group top-up terms needed), pay, approve → invite code.
        await using (var db = fx.NewDb())
        {
            var svc = fx.Bookings(db, clock);
            await svc.SubmitDetailsAsync(hold.AccessToken, new SubmitDetailsRequest("أحمد علي", NextPhone(), null, null, true, false, hold.Price), default);
            using var png = DatabaseFixture.TinyPng();
            await svc.SubmitPaymentProofAsync(hold.AccessToken, await DatabaseFixture.PaymentMethodIdAsync(db), png, png.Length, null, default);
            var proofId = await db.PaymentProofs.Where(p => p.BookingId == hold.BookingId).Select(p => p.Id).SingleAsync();
            await fx.Payments(db, clock).ApproveAsync(proofId, Admin, default);
        }
        string code;
        await using (var db = fx.NewDb())
        {
            var view = await fx.Bookings(db, clock).GetViewAsync(hold.AccessToken, default);
            Assert.True(view.IsClosed);
            Assert.NotNull(view.Invite);
            Assert.Equal(3, view.Invite!.Seats);
            code = view.Invite.Code;
            var info = await fx.Private(db, clock).InviteInfoAsync(code.ToLowerInvariant(), default);
            Assert.Equal("أحمد", info.OrganizerFirstName);
            Assert.Equal(2, info.SeatsLeft);
        }

        // Two friends join for free; a third is refused (3 seats paid in total).
        for (var i = 0; i < 2; i++)
        {
            await using var db = fx.NewDb();
            var joined = await fx.Private(db, clock).JoinAsync(code, new JoinGroupRequest($"صاحب {i}", NextPhone(), null, null, true), default);
            var friendView = await fx.Bookings(db, clock).GetViewAsync(joined.AccessToken, default);
            Assert.Equal("Confirmed", friendView.Status);
            Assert.Equal(0, friendView.Price);
            Assert.Equal("أحمد", friendView.InvitedBy);
        }
        await using (var db = fx.NewDb())
        {
            var ex = await Assert.ThrowsAsync<DomainException>(() =>
                fx.Private(db, clock).JoinAsync(code, new JoinGroupRequest("زيادة", NextPhone(), null, null, true), default));
            Assert.Equal(BookingErrors.SessionFull, ex.Code);
            var view = await fx.Bookings(db, clock).GetViewAsync(hold.AccessToken, default);
            Assert.Equal(3, view.Invite!.Joined);
        }
    }

    [Fact]
    public async Task Teacher_proposal_stays_a_draft_until_the_admin_publishes_it()
    {
        RequireDb();
        var clock = new FakeClock(fx.SeedTime);
        await using var db = fx.NewDb();
        var gs = await (from x in db.GradeSubjects join g in db.Grades on x.GradeId equals g.Id where g.Stage!.NameAr == "ثانوي" select x).FirstAsync();
        var track = await db.StudyTracks.FirstAsync();
        var service = await db.ServiceTypes.FirstAsync(s => s.Code == "one_lesson");
        var teacher = await db.Teachers.FirstAsync();
        var sessions = fx.AdminSessions(db, clock);
        var preview = await sessions.PreviewPricingAsync(new PricingPreviewRequest(service.Id, gs.GradeId, track.Id, gs.SubjectId, "Group"), default);
        var tier = preview.Tiers.Last();

        var id = await sessions.ProposeAsync(teacher.Id, new TeacherProposalRequest(service.Id, gs.GradeId, track.Id, gs.SubjectId,
            "مراجعة التفاضل — طلب كبير", null, fx.SeedTime.AddDays(6), 90, tier.Id!.Value), "teacher@test", default);
        var mine = await sessions.ProposalsAsync(teacher.Id, default);
        Assert.Equal("Pending", mine.Single(p => p.Id == id).State);
        Assert.Contains(await sessions.ListAsync("Proposed", false, default), s => s.Id == id && s.ProposedByTeacherName != null);
        Assert.DoesNotContain(await new Dose.Application.Sessions.SessionQueries(db, clock).ListAsync(new(null, null, null, null, null), default), s => s.Id == id);

        await sessions.PublishAsync(id, Admin, default);
        Assert.Equal("Approved", (await sessions.ProposalsAsync(teacher.Id, default)).Single(p => p.Id == id).State);
    }
}
