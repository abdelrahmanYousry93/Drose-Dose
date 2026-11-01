using Dose.Application.Abstractions;
using Dose.Application.Bookings;
using Dose.Application.Catalog;
using Dose.Application.Notifications;
using Dose.Contracts;
using Dose.Domain.Common;
using Dose.Domain.Notifications;
using Dose.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Dose.IntegrationTests;

[Collection(DbCollection.Name)]
public sealed class MessagingTests(DatabaseFixture fx)
{
    private const string Admin = "admin@test";
    private static int _phone = 500;

    private static void RequireDb() =>
        Assert.SkipWhen(DatabaseFixture.ServerConnection is null, "Set DOSE_TEST_SQL to run SQL Server integration tests.");

    private static string NextPhone() => $"0111{Interlocked.Increment(ref _phone):0000000}";

    private async Task<(string Token, Guid BookingId, Guid ProofId)> BookWithProofAsync(Guid sessionId, FakeClock clock, IRequestLanguage? lang = null)
    {
        await using var db = fx.NewDb();
        var svc = fx.Bookings(db, clock, lang);
        var hold = await svc.CreateHoldAsync(sessionId, default);
        await svc.SubmitDetailsAsync(hold.AccessToken, new SubmitDetailsRequest("نور", NextPhone(), null, null, true, true, hold.Price), default);
        using var png = DatabaseFixture.TinyPng();
        await svc.SubmitPaymentProofAsync(hold.AccessToken, await DatabaseFixture.PaymentMethodIdAsync(db), png, png.Length, null, default);
        var proofId = await db.PaymentProofs.Where(p => p.BookingId == hold.BookingId).Select(p => p.Id).SingleAsync();
        return (hold.AccessToken, hold.BookingId, proofId);
    }

    private async Task<List<Notification>> MessagesForAsync(Guid bookingId)
    {
        await using var db = fx.NewDb();
        return await db.Notifications.AsNoTracking().Where(n => n.BookingId == bookingId).ToListAsync();
    }

    [Fact]
    public async Task Approval_queues_one_confirmation_with_the_students_private_link()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group);
        var clock = new FakeClock(fx.SeedTime);
        var (token, bookingId, proofId) = await BookWithProofAsync(session.Id, clock);

        for (var i = 0; i < 2; i++)
        {
            await using var db = fx.NewDb();
            await fx.Payments(db, clock).ApproveAsync(proofId, Admin, default);
        }

        var msg = Assert.Single(await MessagesForAsync(bookingId));
        Assert.Equal(TemplateCodes.BookingConfirmed, msg.TemplateCode);
        Assert.Equal(NotificationStatus.Pending, msg.Status);
        Assert.Contains("{link}", msg.Body);              // link is never stored
        Assert.DoesNotContain(token, msg.Body);

        await using var db2 = fx.NewDb();
        var notify = fx.Notify(db2, clock);
        var composed = await notify.ComposeAsync(msg.Id, default);
        Assert.Contains($"https://dose.test/b/{token}", composed.Text); // same link the student already has
        Assert.StartsWith("https://wa.me/20", composed.ChatUrl);
        Assert.DoesNotContain("text=", composed.ChatUrl);

        await notify.MarkAsync(msg.Id, true, Admin, default);
        var sent = Assert.Single(await MessagesForAsync(bookingId));
        Assert.Equal(NotificationStatus.Sent, sent.Status);
        Assert.Equal(Admin, sent.HandledBy);
    }

    [Fact]
    public async Task English_booking_gets_english_message_and_rejection_reason()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group);
        var clock = new FakeClock(fx.SeedTime);
        var (_, bookingId, proofId) = await BookWithProofAsync(session.Id, clock, FixedLanguage.English);

        await using (var db = fx.NewDb())
            await fx.Payments(db, clock).RejectAsync(proofId, "blurry image", Admin, default);

        var msg = Assert.Single(await MessagesForAsync(bookingId));
        Assert.Equal(TemplateCodes.PaymentRejected, msg.TemplateCode);
        Assert.Equal("en", msg.Language);
        Assert.Contains("blurry image", msg.Body);
        Assert.StartsWith("Hi نور", msg.Body);
    }

    [Fact]
    public async Task Group_shortfall_queues_top_up_message_with_amount()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group, "جروب متوسط (5–9)");
        var clock = new FakeClock(fx.SeedTime);
        var (_, bookingId, proofId) = await BookWithProofAsync(session.Id, clock);
        await using (var db = fx.NewDb()) await fx.Payments(db, clock).ApproveAsync(proofId, Admin, default);

        clock.UtcNow = session.TierDecisionAtUtc!.Value.AddMinutes(1);
        await using (var db = fx.NewDb()) await fx.Decisions(db, clock).RunDueDecisionsAsync(default);

        var topUp = Assert.Single(await MessagesForAsync(bookingId), m => m.TemplateCode == TemplateCodes.TopUpRequired);
        Assert.Contains("50 ج.م", topUp.Body); // alone → private 150 − paid 100
    }

    [Fact]
    public async Task Reminders_are_queued_once_for_sessions_starting_soon()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group);
        var clock = new FakeClock(fx.SeedTime);
        var (_, bookingId, proofId) = await BookWithProofAsync(session.Id, clock);
        await using (var db = fx.NewDb()) await fx.Payments(db, clock).ApproveAsync(proofId, Admin, default);

        await using (var db = fx.NewDb()) await fx.Notify(db, clock).QueueRemindersAsync(default);
        Assert.DoesNotContain(await MessagesForAsync(bookingId), m => m.TemplateCode == TemplateCodes.SessionReminder);

        clock.UtcNow = session.StartUtc.AddHours(-2);
        for (var i = 0; i < 2; i++)
        {
            await using var db = fx.NewDb();
            await fx.Notify(db, clock).QueueRemindersAsync(default);
        }
        Assert.Single(await MessagesForAsync(bookingId), m => m.TemplateCode == TemplateCodes.SessionReminder);
    }

    [Fact]
    public async Task Templates_validate_placeholders_and_inactive_templates_are_not_queued()
    {
        RequireDb();
        var clock = new FakeClock(fx.SeedTime);
        await using var db = fx.NewDb();
        var notify = fx.Notify(db, clock);
        var templates = await notify.TemplatesAsync(default);
        var t = templates.Single(x => x.Code == TemplateCodes.RequestReceived);
        Assert.Contains("reference", t.Placeholders);

        var bad = await Assert.ThrowsAsync<DomainException>(() =>
            notify.SaveTemplateAsync(t.Code, new SaveTemplateRequest("أهلاً {student_name} {iban}", t.BodyEn, true), Admin, default));
        Assert.Contains("{iban}", bad.Text.Ar);
        Assert.Contains("{iban}", bad.Text.En);

        await notify.SaveTemplateAsync(t.Code, new SaveTemplateRequest(t.BodyAr, t.BodyEn, false), Admin, default);
        try
        {
            var grade = await db.Grades.Include(g => g.Stage).FirstAsync(g => g.Stage!.NameAr == "ثانوي");
            var subject = await db.GradeSubjects.Where(x => x.GradeId == grade.Id).Select(x => x.SubjectId).FirstAsync();
            var created = await fx.Requests(db, clock).CreateAsync(new CreateSessionRequest("ليلى", "01277788899", null, null, grade.Id,
                (await db.StudyTracks.FirstAsync()).Id, subject, (await db.ServiceTypes.FirstAsync()).Id, "مراجعة", ["أي يوم"], ["أي وقت"],
                "Private", null, null), default);
            var requestId = await db.SessionRequests.Where(r => r.Reference == created.Reference).Select(r => r.Id).SingleAsync();
            Assert.False(await db.Notifications.AnyAsync(n => n.SessionRequestId == requestId));
        }
        finally
        {
            await notify.SaveTemplateAsync(t.Code, new SaveTemplateRequest(t.BodyAr, t.BodyEn, true), Admin, default);
        }
    }

    [Fact]
    public async Task Teacher_photo_upload_is_validated_and_served_only_when_published()
    {
        RequireDb();
        var clock = new FakeClock(fx.SeedTime);
        await using var db = fx.NewDb();
        var teacher = new Dose.Domain.Teachers.Teacher { DisplayNameAr = "أ. صورة", IsPublished = false };
        db.Teachers.Add(teacher);
        await db.SaveChangesAsync();
        var catalog = fx.Catalog(db, clock);

        using (var fake = new MemoryStream("<svg onload=alert(1)>"u8.ToArray()))
        {
            var ex = await Assert.ThrowsAsync<DomainException>(() => catalog.UploadTeacherPhotoAsync(teacher.Id, fake, fake.Length, Admin, default));
            Assert.Equal("admin.invalid", ex.Code);
        }
        using (var png = DatabaseFixture.TinyPng())
            await catalog.UploadTeacherPhotoAsync(teacher.Id, png, png.Length, Admin, default);

        Assert.Null(await catalog.OpenTeacherPhotoAsync(teacher.Id, default)); // not published yet
        await db.Teachers.Where(t => t.Id == teacher.Id).ExecuteUpdateAsync(u => u.SetProperty(t => t.IsPublished, true));
        var photo = await catalog.OpenTeacherPhotoAsync(teacher.Id, default);
        Assert.NotNull(photo);
        Assert.Equal("image/png", photo.Value.ContentType);
        await photo.Value.Content.DisposeAsync();

        var listed = (await catalog.TeachersAsync(default)).Single(t => t.Id == teacher.Id);
        Assert.True(listed.HasUploadedPhoto);
        Assert.Equal($"/api/teachers/{teacher.Id}/photo?v=1", listed.PhotoPreviewUrl);
    }

    [Fact]
    public async Task Public_catalog_follows_request_language_with_arabic_fallback()
    {
        RequireDb();
        await using var db = fx.NewDb();
        var subject = await db.Subjects.FirstAsync(s => s.NameAr == "رياضيات");
        subject.NameEn = "Mathematics";
        await db.SaveChangesAsync();

        var en = await new CatalogQueries(db, FixedLanguage.English).SubjectsAsync(null, default);
        var ar = await new CatalogQueries(db, FixedLanguage.Arabic).SubjectsAsync(null, default);
        Assert.Contains(en, s => s.Name == "Mathematics");
        Assert.Contains(en, s => s.Name == "علوم"); // no English name yet → Arabic
        Assert.Contains(ar, s => s.Name == "رياضيات");
    }
}
