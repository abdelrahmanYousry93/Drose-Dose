using Dose.Application.Bookings;
using Dose.Application.Sessions;
using Dose.Contracts;
using Dose.Contracts.Admin;
using Dose.Domain.Bookings;
using Dose.Domain.Common;
using Dose.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Dose.IntegrationTests;

[Collection(DbCollection.Name)]
public sealed class GroupDecisionTests(DatabaseFixture fx)
{
    private const string Admin = "admin@test";
    private static int _phone = 100;

    private static void RequireDb() =>
        Assert.SkipWhen(DatabaseFixture.ServerConnection is null, "Set DOSE_TEST_SQL to run SQL Server integration tests.");

    private static string NextPhone() => $"0100{Interlocked.Increment(ref _phone):0000000}";

    /// <summary>Hold → details → proof, optionally approved. Returns the secret token.</summary>
    private async Task<string> BookAsync(Guid sessionId, FakeClock clock, bool approve = true)
    {
        string token;
        await using (var db = fx.NewDb())
        {
            var svc = fx.Bookings(db, clock);
            var hold = await svc.CreateHoldAsync(sessionId, default);
            token = hold.AccessToken;
            await svc.SubmitDetailsAsync(token, new SubmitDetailsRequest("طالب", NextPhone(), null, null, true, true, hold.Price), default);
            using var png = DatabaseFixture.TinyPng();
            await svc.SubmitPaymentProofAsync(token, await DatabaseFixture.PaymentMethodIdAsync(db), png, png.Length, null, default);
        }
        if (approve) await ApproveMainProofAsync(token, clock);
        return token;
    }

    private async Task ApproveMainProofAsync(string token, FakeClock clock)
    {
        await using var db = fx.NewDb();
        var hash = AccessTokens.Hash(token);
        var proofId = await db.PaymentProofs.Where(p => p.Booking!.AccessTokenHash == hash && p.AdjustmentId == null)
            .Select(p => p.Id).SingleAsync();
        await fx.Payments(db, clock).ApproveAsync(proofId, Admin, default);
    }

    private async Task<BookingViewDto> ViewAsync(string token, FakeClock clock)
    {
        await using var db = fx.NewDb();
        return await fx.Bookings(db, clock).GetViewAsync(token, default);
    }

    private async Task<Session> SessionAsync(Guid id)
    {
        await using var db = fx.NewDb();
        return await db.Sessions.AsNoTracking().SingleAsync(s => s.Id == id);
    }

    private async Task DecideAsync(Session s, FakeClock clock)
    {
        clock.UtcNow = s.TierDecisionAtUtc!.Value.AddMinutes(1);
        await using var db = fx.NewDb();
        await fx.Decisions(db, clock).RunDueDecisionsAsync(default);
    }

    [Fact]
    public async Task Target_reached_confirms_session_without_any_charge()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group, "جروب صغير (2–4)");
        var clock = new FakeClock(fx.SeedTime);
        var t1 = await BookAsync(session.Id, clock);
        await BookAsync(session.Id, clock);

        await DecideAsync(session, clock);

        Assert.Equal(SessionStatus.Confirmed, (await SessionAsync(session.Id)).Status);
        var v = await ViewAsync(t1, clock);
        Assert.Equal("Confirmed", v.Status);
        Assert.Null(v.TopUp);
        await using var db = fx.NewDb();
        Assert.False(await db.FinancialAdjustments.AnyAsync(a => a.Booking!.SessionId == session.Id));
    }

    [Fact]
    public async Task Short_group_asks_each_student_for_the_difference_and_is_idempotent()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group, "جروب متوسط (5–9)"); // 100 each, needs 5
        var clock = new FakeClock(fx.SeedTime);
        var tokens = new List<string>();
        for (var i = 0; i < 3; i++) tokens.Add(await BookAsync(session.Id, clock));

        await DecideAsync(session, clock);
        await using (var db = fx.NewDb())
            await fx.Decisions(db, clock).DecideAsync(session.Id, "system", default); // re-run: no effect

        Assert.Equal(SessionStatus.TopUpRequired, (await SessionAsync(session.Id)).Status);
        foreach (var t in tokens)
        {
            var v = await ViewAsync(t, clock);
            Assert.Equal("TopUpPending", v.Status);
            Assert.Equal(20m, v.TopUp!.Amount); // 3 students → small tier 120 − paid 100
            Assert.False(v.CanJoinNow);
        }
        await using var check = fx.NewDb();
        Assert.Equal(3, await check.FinancialAdjustments.CountAsync(a => a.Booking!.SessionId == session.Id));
    }

    [Fact]
    public async Task Student_choices_pay_refund_transfer_and_admin_resolution()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group, "جروب متوسط (5–9)");
        var clock = new FakeClock(fx.SeedTime);
        var payer = await BookAsync(session.Id, clock);
        var refunder = await BookAsync(session.Id, clock);
        var mover = await BookAsync(session.Id, clock);
        var waived = await BookAsync(session.Id, clock);
        await DecideAsync(session, clock);
        Assert.Equal(20m, (await ViewAsync(payer, clock)).TopUp!.Amount); // 4 → small tier 120

        await using (var db = fx.NewDb())
        {
            var svc = fx.Bookings(db, clock);
            using var png = DatabaseFixture.TinyPng();
            await svc.SubmitTopUpProofAsync(payer, await DatabaseFixture.PaymentMethodIdAsync(db), png, png.Length, "TX-TOPUP", default);
            await svc.ChooseTopUpAsync(refunder, "Refund", default);
            await svc.ChooseTopUpAsync(mover, "Transfer", default);
        }

        // Opting out never raises what the others owe.
        Assert.Equal(20m, (await ViewAsync(payer, clock)).TopUp!.Amount);
        Assert.Equal("Submitted", (await ViewAsync(payer, clock)).TopUp!.ProofStatus);

        await using (var db = fx.NewDb())
        {
            var hash = AccessTokens.Hash(payer);
            var proof = await db.PaymentProofs.SingleAsync(p => p.Booking!.AccessTokenHash == hash && p.AdjustmentId != null);
            Assert.Equal(20m, proof.Amount);
            await fx.Payments(db, clock).ApproveAsync(proof.Id, Admin, default);
        }
        Assert.Equal("Confirmed", (await ViewAsync(payer, clock)).Status);

        await using (var db = fx.NewDb())
        {
            var list = await fx.FollowUps(db, clock).ListAsync(default);
            var mine = list.Where(f => f.SessionId == session.Id).ToList();
            Assert.Contains(mine, f => f.Kind == "Refund");
            Assert.Contains(mine, f => f.Kind == "Transfer");
            var waive = mine.Single(f => f.Kind == "TopUp");

            await fx.FollowUps(db, clock).WaiveAsync(waive.BookingId, "أول جلسة", Admin, default);
            await fx.FollowUps(db, clock).MarkRefundedAsync(mine.Single(f => f.Kind == "Refund").BookingId, "حوّلنا على نفس الرقم", Admin, default);
            await fx.FollowUps(db, clock).MarkTransferDoneAsync(mine.Single(f => f.Kind == "Transfer").BookingId, "اتنقل لجلسة الخميس", Admin, default);
        }

        Assert.Equal("Confirmed", (await ViewAsync(waived, clock)).Status);
        Assert.Equal("Refunded", (await ViewAsync(refunder, clock)).Status);
        Assert.Equal(SessionStatus.Confirmed, (await SessionAsync(session.Id)).Status);
        await using (var db = fx.NewDb())
        {
            var hash = AccessTokens.Hash(refunder);
            var refund = await db.FinancialAdjustments.SingleAsync(a => a.Booking!.AccessTokenHash == hash && a.Type == AdjustmentType.Refund);
            Assert.Equal(100m, refund.Amount);
            Assert.DoesNotContain((await fx.FollowUps(db, clock).ListAsync(default)), f => f.SessionId == session.Id);
        }
    }

    [Fact]
    public async Task Late_approval_that_reaches_the_target_cancels_everyones_top_up()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group, "جروب متوسط (5–9)");
        var clock = new FakeClock(fx.SeedTime);
        var tokens = new List<string>();
        for (var i = 0; i < 4; i++) tokens.Add(await BookAsync(session.Id, clock));
        var late = await BookAsync(session.Id, clock, approve: false); // still under review at decision time

        await DecideAsync(session, clock);
        Assert.Equal(20m, (await ViewAsync(tokens[0], clock)).TopUp!.Amount);

        await ApproveMainProofAsync(late, clock); // now 5 confirmed → target tier reached

        foreach (var t in tokens.Append(late))
        {
            var v = await ViewAsync(t, clock);
            Assert.Equal("Confirmed", v.Status);
            Assert.Null(v.TopUp);
        }
        Assert.Equal(SessionStatus.Confirmed, (await SessionAsync(session.Id)).Status);
    }

    [Fact]
    public async Task Student_cancellation_rules()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group, "جروب كبير (10–15)");
        var clock = new FakeClock(fx.SeedTime);
        var early = await BookAsync(session.Id, clock);
        var late = await BookAsync(session.Id, clock);
        string unpaid;
        await using (var db = fx.NewDb())
        {
            var hold = await fx.Bookings(db, clock).CreateHoldAsync(session.Id, default);
            unpaid = hold.AccessToken;
        }

        await using (var db = fx.NewDb())
        {
            var svc = fx.Bookings(db, clock);
            await svc.CancelAsync(early, false, default);
            await svc.CancelAsync(unpaid, false, default);
        }
        Assert.Equal("RefundPending", (await ViewAsync(early, clock)).Status);
        Assert.Equal("Cancelled", (await ViewAsync(unpaid, clock)).Status);

        clock.UtcNow = session.StartUtc.AddHours(-3); // inside the no-refund window (default 24h)
        await using (var db = fx.NewDb())
        {
            var ex = await Assert.ThrowsAsync<DomainException>(() => fx.Bookings(db, clock).CancelAsync(late, false, default));
            Assert.Equal(BookingErrors.CancelNotAllowed, ex.Code);
            await fx.Bookings(db, clock).CancelAsync(late, true, default);
        }
        var v = await ViewAsync(late, clock);
        Assert.Equal("Cancelled", v.Status);
        Assert.False(v.CanCancel);
    }

    [Fact]
    public async Task Session_request_with_indicative_prices_and_admin_follow_up()
    {
        RequireDb();
        var clock = new FakeClock(fx.SeedTime);
        await using var db = fx.NewDb();
        var prep = await db.Grades.Include(g => g.Stage).FirstAsync(g => g.Stage!.NameAr == "إعدادي");
        var subject = await db.GradeSubjects.Where(x => x.GradeId == prep.Id).Select(x => x.SubjectId).FirstAsync();
        var track = await db.StudyTracks.FirstAsync();
        var service = await db.ServiceTypes.SingleAsync(s => s.Code == "one_lesson");
        var svc = new SessionRequestService(db, clock);

        var options = await svc.PriceOptionsAsync(service.Id, prep.Id, track.Id, subject, default);
        Assert.NotNull(options);
        Assert.Equal(150m, options!.PrivatePrice);
        Assert.Equal(3, options.GroupOptions.Count);

        var req = new CreateSessionRequest("ياسين", "01155667788", null, null, prep.Id, track.Id, subject, service.Id,
            "الكسور العشرية", ["السبت", "الاثنين"], ["مساءً"], "Group", options.GroupOptions[0].Id, null);
        var guardian = await Assert.ThrowsAsync<DomainException>(() => svc.CreateAsync(req, default));
        Assert.Equal(BookingErrors.GuardianRequired, guardian.Code);

        var created = await svc.CreateAsync(req with { GuardianName = "ولي أمر", GuardianPhone = "01155667789" }, default);
        Assert.StartsWith("RQ-", created.Reference);

        var list = await svc.ListAsync("New", default);
        var mine = list.Single(r => r.Reference == created.Reference);
        Assert.Equal("السبت، الاثنين", mine.PreferredDays);
        Assert.Equal("جروب صغير (2–4)", mine.PreferredTierNameAr);

        await svc.UpdateStatusAsync(mine.Id, new UpdateRequestStatus("Contacted", "اتكلمنا معاه", null), Admin, default);
        Assert.Equal("Contacted", (await svc.ListAsync(null, default)).Single(r => r.Id == mine.Id).Status);
    }
}
