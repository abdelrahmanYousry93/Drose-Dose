using Dose.Application.Administration;
using Dose.Application.Bookings;
using Dose.Application.Teachers;
using Dose.Contracts;
using Dose.Contracts.Admin;
using Dose.Domain.Bookings;
using Dose.Domain.Common;
using Dose.Domain.Sessions;
using Dose.Domain.Teachers;
using Dose.Infrastructure.Identity;
using Dose.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dose.IntegrationTests;

[Collection(DbCollection.Name)]
public sealed class Phase4Tests(DatabaseFixture fx)
{
    private const string Admin = "admin@test";

    private static void RequireDb() =>
        Assert.SkipWhen(DatabaseFixture.ServerConnection is null, "Set DOSE_TEST_SQL to run SQL Server integration tests.");

    private async Task<(Guid TeacherId, string UserId)> NewTeacherAsync(string name)
    {
        await using var db = fx.NewDb();
        var userId = Guid.NewGuid().ToString();
        var t = new Teacher { DisplayNameAr = name, IsPublished = true, UserId = userId };
        db.Teachers.Add(t);
        await db.SaveChangesAsync();
        return (t.Id, userId);
    }

    private async Task<Session> SessionForAsync(Guid teacherId, string tier = "جروب صغير (2–4)")
    {
        var s = await fx.CreateSessionAsync(SessionMode.Group, tier);
        await using var db = fx.NewDb();
        await db.Sessions.Where(x => x.Id == s.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.TeacherId, teacherId));
        return s;
    }

    private async Task<string> ConfirmedBookingAsync(Guid sessionId, string name, string phone, FakeClock clock)
    {
        string token;
        await using (var db = fx.NewDb())
        {
            var svc = fx.Bookings(db, clock);
            var hold = await svc.CreateHoldAsync(sessionId, default);
            token = hold.AccessToken;
            await svc.SubmitDetailsAsync(token, new SubmitDetailsRequest(name, phone, null, null, true, true, hold.Price), default);
            using var png = DatabaseFixture.TinyPng();
            await svc.SubmitPaymentProofAsync(token, await DatabaseFixture.PaymentMethodIdAsync(db), png, png.Length, null, default);
        }
        await using (var db = fx.NewDb())
        {
            var hash = AccessTokens.Hash(token);
            var proof = await db.PaymentProofs.SingleAsync(p => p.Booking!.AccessTokenHash == hash);
            await fx.Payments(db, clock).ApproveAsync(proof.Id, Admin, default);
        }
        return token;
    }

    [Fact]
    public async Task Teacher_sees_own_sessions_with_first_names_marks_attendance_and_completes()
    {
        RequireDb();
        var (teacherId, userId) = await NewTeacherAsync("أ. مدرس الحضور");
        var (_, otherUser) = await NewTeacherAsync("أ. مدرس تاني");
        var session = await SessionForAsync(teacherId);
        var clock = new FakeClock(fx.SeedTime);
        await ConfirmedBookingAsync(session.Id, "سلمى أحمد علي", "01020304050", clock);
        await ConfirmedBookingAsync(session.Id, "كريم محمود", "01020304060", clock);

        await using var db = fx.NewDb();
        var svc = new TeacherAreaService(db, clock);
        var list = await svc.SessionsAsync(userId, false, default);
        var mine = Assert.Single(list, s => s.Id == session.Id);
        Assert.Equal(2, mine.ConfirmedCount);
        Assert.Equal("https://meet.google.com/test-room", mine.MeetingUrl);
        Assert.False(mine.CanMarkAttendance);

        var details = await svc.DetailsAsync(userId, session.Id, default);
        Assert.Equal(["سلمى", "كريم"], details.Students.Select(s => s.FirstName).ToArray());

        var notYours = await Assert.ThrowsAsync<DomainException>(() => svc.DetailsAsync(otherUser, session.Id, default));
        Assert.Equal(TeacherErrors.NotYours, notYours.Code);
        var notLinked = await Assert.ThrowsAsync<DomainException>(() => svc.SessionsAsync("no-such-user", false, default));
        Assert.Equal(TeacherErrors.NotLinked, notLinked.Code);

        var items = details.Students.Select((s, i) => new AttendanceItem(s.BookingId, i == 0)).ToList();
        var early = await Assert.ThrowsAsync<DomainException>(() => svc.MarkAttendanceAsync(userId, session.Id, new MarkAttendanceRequest(items), default));
        Assert.Equal(TeacherErrors.Invalid, early.Code);

        clock.UtcNow = session.StartUtc.AddMinutes(5);
        await svc.MarkAttendanceAsync(userId, session.Id, new MarkAttendanceRequest(items.Take(1).ToList()), default);
        var incomplete = await Assert.ThrowsAsync<DomainException>(() => svc.CompleteAsync(userId, session.Id, default));
        Assert.Equal(TeacherErrors.Invalid, incomplete.Code);

        await svc.MarkAttendanceAsync(userId, session.Id, new MarkAttendanceRequest(items), default);
        await svc.CompleteAsync(userId, session.Id, default);

        await using var check = fx.NewDb();
        Assert.Equal(SessionStatus.Completed, (await check.Sessions.SingleAsync(s => s.Id == session.Id)).Status);
        var statuses = await check.Bookings.Where(b => b.SessionId == session.Id).OrderBy(b => b.CreatedAtUtc).Select(b => b.Status).ToListAsync();
        Assert.Equal([BookingStatus.Completed, BookingStatus.NoShow], statuses);
    }

    [Fact]
    public async Task Availability_submit_overlap_review_and_withdraw()
    {
        RequireDb();
        var (_, userId) = await NewTeacherAsync("أ. مدرس المواعيد");
        var clock = new FakeClock(fx.SeedTime);
        await using var db = fx.NewDb();
        var svc = new TeacherAreaService(db, clock);
        var start = fx.SeedTime.AddDays(2).AddHours(14);

        var id = await svc.SubmitAvailabilityAsync(userId, new SubmitAvailabilityRequest(start, start.AddHours(3), "بعد الضهر"), default);
        var overlap = await Assert.ThrowsAsync<DomainException>(() =>
            svc.SubmitAvailabilityAsync(userId, new SubmitAvailabilityRequest(start.AddHours(1), start.AddHours(4), null), default));
        Assert.Equal(TeacherErrors.Invalid, overlap.Code);
        var past = await Assert.ThrowsAsync<DomainException>(() =>
            svc.SubmitAvailabilityAsync(userId, new SubmitAvailabilityRequest(fx.SeedTime.AddHours(-2), fx.SeedTime.AddHours(-1), null), default));
        Assert.Equal(TeacherErrors.Invalid, past.Code);

        var pending = await svc.AdminListAvailabilityAsync("Submitted", default);
        Assert.Contains(pending, a => a.Id == id && a.TeacherNameAr == "أ. مدرس المواعيد");
        await svc.ReviewAvailabilityAsync(id, new ReviewAvailabilityRequest(true, null), Admin, default);
        Assert.Equal("Approved", (await svc.AvailabilityAsync(userId, default)).Single(a => a.Id == id).Status);

        await svc.WithdrawAvailabilityAsync(userId, id, default);
        Assert.Equal("Withdrawn", (await svc.AvailabilityAsync(userId, default)).Single(a => a.Id == id).Status);
    }

    [Fact]
    public async Task Dashboard_reports_attention_items_and_upcoming_groups()
    {
        RequireDb();
        var (teacherId, _) = await NewTeacherAsync("أ. مدرس الداشبورد");
        var session = await SessionForAsync(teacherId, "جروب متوسط (5–9)");
        var clock = new FakeClock(fx.SeedTime);
        await using (var db = fx.NewDb())
        {
            var svc = fx.Bookings(db, clock);
            var hold = await svc.CreateHoldAsync(session.Id, default);
            await svc.SubmitDetailsAsync(hold.AccessToken, new SubmitDetailsRequest("طالب", "01099887766", null, null, true, true, hold.Price), default);
            using var png = DatabaseFixture.TinyPng();
            await svc.SubmitPaymentProofAsync(hold.AccessToken, await DatabaseFixture.PaymentMethodIdAsync(db), png, png.Length, null, default);
        }

        clock.UtcNow = session.TierDecisionAtUtc!.Value.AddHours(-12); // decision within the next 48h
        await using var db2 = fx.NewDb();
        var dash = await new DashboardService(db2, clock).GetAsync(30, default);
        Assert.True(dash.PendingProofs >= 1);
        Assert.NotNull(dash.OldestPendingProofMinutes);
        var group = Assert.Single(dash.GroupsNearDecision, g => g.Id == session.Id);
        Assert.Equal(5, group.MinParticipants);
        Assert.Equal(0, group.ConfirmedCount);
    }

    [Fact]
    public async Task User_management_safeguards()
    {
        RequireDb();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<DoseDbContext>(o => o.UseSqlServer(fx.ConnectionString));
        services.AddIdentityCore<IdentityUser>(o => o.Password.RequiredLength = 10)
            .AddRoles<IdentityRole>().AddEntityFrameworkStores<DoseDbContext>().AddDefaultTokenProviders();
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        await DevelopmentSeeder.SeedRolesAndAdminAsync(roles, users, null, null);
        var db = scope.ServiceProvider.GetRequiredService<DoseDbContext>();
        var svc = new UserAdminService(users, db, new FakeClock(fx.SeedTime));

        var adminId = await svc.CreateAsync(new CreateUserRequest("Boss@Dose.test", "Strong_Pass_123", "Admin", null), Admin, default);
        var dup = await Assert.ThrowsAsync<DomainException>(() => svc.CreateAsync(new CreateUserRequest("boss@dose.test", "Strong_Pass_123", "Admin", null), Admin, default));
        Assert.Equal(AdminErrors.Conflict, dup.Code);
        var weak = await Assert.ThrowsAsync<DomainException>(() => svc.CreateAsync(new CreateUserRequest("weak@dose.test", "short", "Admin", null), Admin, default));
        Assert.Contains("10", weak.Message);

        var self = await Assert.ThrowsAsync<DomainException>(() => svc.SetDisabledAsync(adminId, true, adminId, Admin, default));
        Assert.Equal(AdminErrors.Invalid, self.Code);

        var teacher = new Teacher { DisplayNameAr = "أ. حساب مدرس", IsPublished = true };
        db.Teachers.Add(teacher);
        await db.SaveChangesAsync();
        var needsProfile = await Assert.ThrowsAsync<DomainException>(() => svc.CreateAsync(new CreateUserRequest("t@dose.test", "Strong_Pass_123", "Teacher", null), Admin, default));
        Assert.Equal(AdminErrors.Invalid, needsProfile.Code);
        var teacherUser = await svc.CreateAsync(new CreateUserRequest("t@dose.test", "Strong_Pass_123", "Teacher", teacher.Id), Admin, default);
        Assert.Equal(teacherUser, (await db.Teachers.AsNoTracking().SingleAsync(t => t.Id == teacher.Id)).UserId);

        await svc.SetDisabledAsync(teacherUser, true, adminId, Admin, default);
        var listed = await svc.ListAsync(default);
        Assert.True(listed.Single(u => u.Id == teacherUser).IsDisabled);
        Assert.Equal("أ. حساب مدرس", listed.Single(u => u.Id == teacherUser).TeacherNameAr);

        await svc.ResetPasswordAsync(teacherUser, "Another_Pass_456", Admin, default);
        var user = await users.FindByIdAsync(teacherUser);
        Assert.True(await users.CheckPasswordAsync(user!, "Another_Pass_456"));

        // Disabling every other admin leaves this one as the last: it can't be disabled.
        foreach (var other in (await users.GetUsersInRoleAsync("Admin")).Where(u => u.Id != adminId))
            await svc.SetDisabledAsync(other.Id, true, adminId, Admin, default);
        var last = await Assert.ThrowsAsync<DomainException>(() => svc.SetDisabledAsync(adminId, true, "someone-else", Admin, default));
        Assert.Equal(AdminErrors.Invalid, last.Code);
    }
}
