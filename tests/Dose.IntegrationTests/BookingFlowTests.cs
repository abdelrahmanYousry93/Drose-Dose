using Dose.Contracts;
using Dose.Domain.Bookings;
using Dose.Domain.Common;
using Dose.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Dose.IntegrationTests;

[Collection(DbCollection.Name)]
public sealed class BookingFlowTests(DatabaseFixture fx)
{
    private static void RequireDb() =>
        Assert.SkipWhen(DatabaseFixture.ServerConnection is null, "Set DOSE_TEST_SQL to run SQL Server integration tests.");

    private static SubmitDetailsRequest Details(string phone, decimal price, string? guardianName = null, string? guardianPhone = null) =>
        new("طالب اختبار", phone, guardianName, guardianPhone, AcceptTerms: true, AcceptTopUpTerms: true, ExpectedPrice: price);

    private async Task<(string Token, Guid Proof)> BookAndUploadAsync(Guid sessionId, string phone, FakeClock clock)
    {
        string token;
        await using (var db = fx.NewDb())
        {
            var hold = await fx.Bookings(db, clock).CreateHoldAsync(sessionId, default);
            token = hold.AccessToken;
            await fx.Bookings(db, clock).SubmitDetailsAsync(token, Details(phone, hold.Price), default);
        }
        await using (var db = fx.NewDb())
        {
            using var png = DatabaseFixture.TinyPng();
            await fx.Bookings(db, clock).SubmitPaymentProofAsync(token, await DatabaseFixture.PaymentMethodIdAsync(db), png, png.Length, "TX-1", default);
            var hash = Dose.Application.Bookings.AccessTokens.Hash(token);
            var proofId = await db.PaymentProofs.Where(p => p.Booking!.AccessTokenHash == hash).Select(p => p.Id).SingleAsync();
            return (token, proofId);
        }
    }

    [Fact]
    public async Task Concurrent_holds_never_exceed_capacity()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group, "جروب صغير (2–4)"); // capacity 4
        var clock = new FakeClock(fx.SeedTime);

        var attempts = Enumerable.Range(0, 25).Select(async _ =>
        {
            await using var db = fx.NewDb();
            try
            {
                await fx.Bookings(db, clock).CreateHoldAsync(session.Id, default);
                return "ok";
            }
            catch (DomainException ex) { return ex.Code; }
        });
        var results = await Task.WhenAll(attempts);

        Assert.Equal(4, results.Count(r => r == "ok"));
        Assert.Equal(21, results.Count(r => r == BookingErrors.SessionFull));
        await using var check = fx.NewDb();
        Assert.Equal(4, await check.Bookings.CountAsync(b => b.SessionId == session.Id));
    }

    [Fact]
    public async Task Last_seat_race_has_exactly_one_winner()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Private); // capacity 1
        var clock = new FakeClock(fx.SeedTime);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            await using var db = fx.NewDb();
            try { await fx.Bookings(db, clock).CreateHoldAsync(session.Id, default); return true; }
            catch (DomainException ex) when (ex.Code == BookingErrors.SessionFull) { return false; }
        }));
        Assert.Single(results, r => r);
    }

    [Fact]
    public async Task Same_whatsapp_number_cannot_book_the_same_session_twice()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group);
        var clock = new FakeClock(fx.SeedTime);
        await using var db = fx.NewDb();
        var svc = fx.Bookings(db, clock);

        var first = await svc.CreateHoldAsync(session.Id, default);
        await svc.SubmitDetailsAsync(first.AccessToken, Details("01001112223", first.Price), default);

        await using var db2 = fx.NewDb();
        var second = await fx.Bookings(db2, clock).CreateHoldAsync(session.Id, default);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            fx.Bookings(db2, clock).SubmitDetailsAsync(second.AccessToken, Details("+20 100 111 2223", second.Price), default));
        Assert.Equal(BookingErrors.Duplicate, ex.Code);
    }

    [Fact]
    public async Task Expired_hold_releases_the_seat()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Private);
        var clock = new FakeClock(fx.SeedTime);

        await using var db = fx.NewDb();
        var first = await fx.Bookings(db, clock).CreateHoldAsync(session.Id, default);

        await using (var db2 = fx.NewDb())
        {
            var full = await Assert.ThrowsAsync<DomainException>(() => fx.Bookings(db2, clock).CreateHoldAsync(session.Id, default));
            Assert.Equal(BookingErrors.SessionFull, full.Code);
        }

        clock.UtcNow = clock.UtcNow.AddMinutes(16); // default hold is 15 minutes
        await using (var db3 = fx.NewDb())
            await fx.Bookings(db3, clock).CreateHoldAsync(session.Id, default);

        await using var db4 = fx.NewDb();
        var expired = await Assert.ThrowsAsync<DomainException>(() =>
            fx.Bookings(db4, clock).SubmitDetailsAsync(first.AccessToken, Details("01009998887", first.Price), default));
        Assert.Contains(expired.Code, new[] { BookingErrors.Expired, BookingErrors.InvalidState });
    }

    [Fact]
    public async Task Guardian_contact_required_for_preparatory_grade()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group, gradeStage: "إعدادي");
        var clock = new FakeClock(fx.SeedTime);
        await using var db = fx.NewDb();
        var svc = fx.Bookings(db, clock);
        var hold = await svc.CreateHoldAsync(session.Id, default);

        var ex = await Assert.ThrowsAsync<DomainException>(() => svc.SubmitDetailsAsync(hold.AccessToken, Details("01005556667", hold.Price), default));
        Assert.Equal(BookingErrors.GuardianRequired, ex.Code);

        await using var db2 = fx.NewDb();
        await fx.Bookings(db2, clock).SubmitDetailsAsync(hold.AccessToken,
            Details("01005556667", hold.Price, "ولي الأمر", "01005556668"), default);
    }

    [Fact]
    public async Task Group_terms_must_be_accepted()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group);
        var clock = new FakeClock(fx.SeedTime);
        await using var db = fx.NewDb();
        var svc = fx.Bookings(db, clock);
        var hold = await svc.CreateHoldAsync(session.Id, default);
        var ex = await Assert.ThrowsAsync<DomainException>(() => svc.SubmitDetailsAsync(hold.AccessToken,
            Details("01004445556", hold.Price) with { AcceptTopUpTerms = false }, default));
        Assert.Equal(BookingErrors.TermsRequired, ex.Code);
    }

    [Fact]
    public async Task Changed_price_requires_student_reconfirmation()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group);
        var clock = new FakeClock(fx.SeedTime);
        await using var db = fx.NewDb();
        var hold = await fx.Bookings(db, clock).CreateHoldAsync(session.Id, default);

        await db.PricingTiers.Where(t => t.Id == session.TargetTierId)
            .ExecuteUpdateAsync(u => u.SetProperty(t => t.PricePerStudent, 125m));

        await using var db2 = fx.NewDb();
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            fx.Bookings(db2, clock).SubmitDetailsAsync(hold.AccessToken, Details("01003334445", hold.Price), default));
        Assert.Equal(BookingErrors.PriceChanged, ex.Code);

        await using var db3 = fx.NewDb();
        await fx.Bookings(db3, clock).SubmitDetailsAsync(hold.AccessToken, Details("01003334445", 125m), default);

        await db3.PricingTiers.Where(t => t.Id == session.TargetTierId)
            .ExecuteUpdateAsync(u => u.SetProperty(t => t.PricePerStudent, 120m));
    }

    [Fact]
    public async Task Approval_confirms_booking_stores_snapshot_and_is_idempotent()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group, "جروب متوسط (5–9)");
        var clock = new FakeClock(fx.SeedTime);
        var (token, proofId) = await BookAndUploadAsync(session.Id, "01007778889", clock);

        // Two admins click approve at the same time.
        await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var db = fx.NewDb();
            await fx.Payments(db, clock).ApproveAsync(proofId, "admin@test", default);
        }));

        await using var check = fx.NewDb();
        var hash = Dose.Application.Bookings.AccessTokens.Hash(token);
        var booking = await check.Bookings.Include(b => b.PriceSnapshot).SingleAsync(b => b.AccessTokenHash == hash);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.NotNull(booking.PriceSnapshot);
        Assert.Equal(100m, booking.PriceSnapshot!.FinalAmount);
        Assert.Equal(150m, booking.PriceSnapshot.WorstCasePrice);
        Assert.Equal(5, booking.PriceSnapshot.TierMin);
        Assert.Equal(1, await check.PriceSnapshots.CountAsync(p => p.Id == booking.PriceSnapshotId));
        Assert.True(await check.PricingPlanVersions.Where(v => v.Id == session.PricingPlanVersionId).Select(v => v.IsLocked).SingleAsync());
    }

    [Fact]
    public async Task Rejected_proof_returns_booking_to_pending_payment()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group);
        var clock = new FakeClock(fx.SeedTime);
        var (token, proofId) = await BookAndUploadAsync(session.Id, "01006667778", clock);

        await using (var db = fx.NewDb())
            await fx.Payments(db, clock).RejectAsync(proofId, "الصورة مش واضحة", "admin@test", default);

        await using var db2 = fx.NewDb();
        var view = await fx.Bookings(db2, clock).GetViewAsync(token, default);
        Assert.Equal(nameof(BookingStatus.PendingPayment), view.Status);
        Assert.Equal("Rejected", view.LatestProofStatus);
        Assert.Equal("الصورة مش واضحة", view.LatestProofRejectionReason);
    }

    [Fact]
    public async Task Meeting_link_only_for_confirmed_booking_inside_join_window()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group);
        var clock = new FakeClock(fx.SeedTime);
        var (token, proofId) = await BookAndUploadAsync(session.Id, "01002223334", clock);

        await using (var db = fx.NewDb())
        {
            var notConfirmed = await Assert.ThrowsAsync<DomainException>(() => fx.Bookings(db, clock).JoinAsync(token, default));
            Assert.Equal(BookingErrors.JoinNotAvailable, notConfirmed.Code);
            await fx.Payments(db, clock).ApproveAsync(proofId, "admin@test", default);
        }

        await using (var db = fx.NewDb())
        {
            var early = await Assert.ThrowsAsync<DomainException>(() => fx.Bookings(db, clock).JoinAsync(token, default));
            Assert.Equal(BookingErrors.JoinNotAvailable, early.Code);
        }

        clock.UtcNow = session.StartUtc.AddMinutes(-10); // default window opens 15 minutes before
        await using (var db = fx.NewDb())
        {
            var join = await fx.Bookings(db, clock).JoinAsync(token, default);
            Assert.Equal("https://meet.google.com/test-room", join.MeetingUrl);
        }
    }

    [Fact]
    public async Task Unknown_or_malformed_token_is_not_found()
    {
        RequireDb();
        var clock = new FakeClock(fx.SeedTime);
        await using var db = fx.NewDb();
        var ex = await Assert.ThrowsAsync<DomainException>(() => fx.Bookings(db, clock).GetViewAsync("x", default));
        Assert.Equal(BookingErrors.NotFound, ex.Code);
        var (other, _) = Dose.Application.Bookings.AccessTokens.Create();
        ex = await Assert.ThrowsAsync<DomainException>(() => fx.Bookings(db, clock).GetViewAsync(other, default));
        Assert.Equal(BookingErrors.NotFound, ex.Code);
    }

    [Fact]
    public async Task Settings_are_admin_editable_and_take_effect()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Private);
        var clock = new FakeClock(fx.SeedTime);
        await using var db = fx.NewDb();
        var settings = new Dose.Application.Administration.SettingsService(db, clock);
        await settings.UpdateAsync(new Dictionary<string, string> { [Dose.Application.Administration.SettingKeys.HoldMinutes] = "30" }, "admin@test", default);
        try
        {
            var hold = await fx.Bookings(db, clock).CreateHoldAsync(session.Id, default);
            Assert.Equal(clock.UtcNow.AddMinutes(30), hold.HoldExpiresAtUtc);

            var bad = await Assert.ThrowsAsync<DomainException>(() => settings.UpdateAsync(
                new Dictionary<string, string> { [Dose.Application.Administration.SettingKeys.HoldMinutes] = "-5" }, "admin@test", default));
            Assert.Equal("settings.invalid_value", bad.Code);
        }
        finally
        {
            await settings.UpdateAsync(new Dictionary<string, string> { [Dose.Application.Administration.SettingKeys.HoldMinutes] = "15" }, "admin@test", default);
        }
    }
}
