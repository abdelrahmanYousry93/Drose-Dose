using Dose.Domain.Bookings;
using Dose.Domain.Catalog;
using Dose.Domain.Pricing;
using Dose.Domain.Sessions;
using Dose.Domain.Teachers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dose.Infrastructure.Persistence;

/// <summary>
/// DEVELOPMENT ONLY. All prices, teachers and payment accounts below are SAMPLE data —
/// not production values. Never run against production.
/// </summary>
public static class DevelopmentSeeder
{
    public const string AdminRole = "Admin";
    public const string TeacherRole = "Teacher";

    public static async Task SeedRolesAndAdminAsync(
        RoleManager<IdentityRole> roles, UserManager<IdentityUser> users, string? adminEmail, string? adminPassword)
    {
        foreach (var r in new[] { AdminRole, TeacherRole })
            if (!await roles.RoleExistsAsync(r)) await roles.CreateAsync(new IdentityRole(r));

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword)) return;
        var user = await users.FindByEmailAsync(adminEmail);
        if (user is null)
        {
            user = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            var result = await users.CreateAsync(user, adminPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException("Could not create admin: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }
        if (!await users.IsInRoleAsync(user, AdminRole)) await users.AddToRoleAsync(user, AdminRole);
    }

    public static async Task SeedSampleDataAsync(DoseDbContext db, DateTime nowUtc)
    {
        if (await db.EducationalStages.AnyAsync()) return;

        // ----- Stages & bands (guardian rule per approved decision: primary + preparatory) -----
        var primary = new EducationalStage { NameAr = "ابتدائي", RequiresGuardianContact = true, SortOrder = 1 };
        var prep = new EducationalStage { NameAr = "إعدادي", RequiresGuardianContact = true, SortOrder = 2 };
        var secondary = new EducationalStage { NameAr = "ثانوي", RequiresGuardianContact = false, SortOrder = 3 };
        db.EducationalStages.AddRange(primary, prep, secondary);

        var bands = new[]
        {
            new PricingBand { NameAr = "ابتدائي مبكر (1–3)", SortOrder = 1 },
            new PricingBand { NameAr = "ابتدائي عليا (4–6)", SortOrder = 2 },
            new PricingBand { NameAr = "إعدادي (1–2)", SortOrder = 3 },
            new PricingBand { NameAr = "الشهادة الإعدادية", SortOrder = 4 },
            new PricingBand { NameAr = "أولى ثانوي", SortOrder = 5 },
            new PricingBand { NameAr = "تانية وتالتة ثانوي", SortOrder = 6 },
        };
        db.PricingBands.AddRange(bands);

        var grades = new List<Grade>();
        string[] primaryNames = ["الأول", "الثاني", "الثالث", "الرابع", "الخامس", "السادس"];
        for (var i = 0; i < 6; i++)
            grades.Add(new Grade { NameAr = $"الصف {primaryNames[i]} الابتدائي", StageId = primary.Id, PricingBandId = bands[i < 3 ? 0 : 1].Id, SortOrder = i + 1 });
        for (var i = 0; i < 3; i++)
            grades.Add(new Grade { NameAr = $"الصف {primaryNames[i]} الإعدادي", StageId = prep.Id, PricingBandId = bands[i < 2 ? 2 : 3].Id, SortOrder = 10 + i });
        for (var i = 0; i < 3; i++)
            grades.Add(new Grade { NameAr = $"الصف {primaryNames[i]} الثانوي", StageId = secondary.Id, PricingBandId = bands[i == 0 ? 4 : 5].Id, SortOrder = 20 + i });
        db.Grades.AddRange(grades);

        var arabicTrack = new StudyTrack { NameAr = "عربي", SortOrder = 1 };
        var langTrack = new StudyTrack { NameAr = "لغات", SortOrder = 2 };
        db.StudyTracks.AddRange(arabicTrack, langTrack);

        var math = new Subject { NameAr = "رياضيات", SortOrder = 1 };
        var science = new Subject { NameAr = "علوم", SortOrder = 2 };
        var physics = new Subject { NameAr = "فيزياء", SortOrder = 3 };
        var english = new Subject { NameAr = "لغة إنجليزية", SortOrder = 4 };
        db.Subjects.AddRange(math, science, physics, english);
        foreach (var g in grades)
        {
            db.GradeSubjects.Add(new GradeSubject { GradeId = g.Id, SubjectId = math.Id });
            db.GradeSubjects.Add(new GradeSubject { GradeId = g.Id, SubjectId = english.Id });
            db.GradeSubjects.Add(new GradeSubject { GradeId = g.Id, SubjectId = g.StageId == secondary.Id ? physics.Id : science.Id });
        }

        // ----- Service types (records, not code) -----
        var oneLesson = new ServiceType { Code = "one_lesson", NameAr = "شرح درس واحد", DescriptionAr = "شرح مركّز لدرس واحد أو جزء محدد منه.", SortOrder = 1 };
        var unit = new ServiceType { Code = "chapter_package", NameAr = "شرح وحدة كاملة", DescriptionAr = "باقة بسعر واحد للوحدة كلها، ممكن تكون أكتر من جلسة.", RequiresPackage = true, SortOrder = 2 };
        var revision = new ServiceType { Code = "general_revision", NameAr = "مراجعة عامة", DescriptionAr = "مراجعة لجزء محدد من المنهج، النطاق مكتوب قبل الحجز.", RequiresPackage = true, SortOrder = 3 };
        db.ServiceTypes.AddRange(oneLesson, unit, revision);

        // ----- Pricing (SAMPLE PRICES — NOT PRODUCTION) -----
        var groupPlan = new PricingPlan
        {
            Name = "SAMPLE — درس واحد جماعي (كل المراحل)", ServiceTypeId = oneLesson.Id, Mode = SessionMode.Group,
            EffectiveFromUtc = nowUtc.AddDays(-1), Policy = PricingPolicy.TargetTierWithTopUp,
        };
        var groupVersion = new PricingPlanVersion { PlanId = groupPlan.Id, VersionNo = 1, PrivatePrice = 150m, IsLocked = true };
        var tierSmall = new PricingTier { VersionId = groupVersion.Id, NameAr = "جروب صغير (2–4)", MinParticipants = 2, MaxParticipants = 4, PricePerStudent = 120m, SortOrder = 1 };
        var tierMedium = new PricingTier { VersionId = groupVersion.Id, NameAr = "جروب متوسط (5–9)", MinParticipants = 5, MaxParticipants = 9, PricePerStudent = 100m, SortOrder = 2 };
        var tierLarge = new PricingTier { VersionId = groupVersion.Id, NameAr = "جروب كبير (10–15)", MinParticipants = 10, MaxParticipants = 15, PricePerStudent = 80m, SortOrder = 3 };
        groupVersion.Tiers.AddRange([tierSmall, tierMedium, tierLarge]);
        groupPlan.Versions.Add(groupVersion);

        var privatePlan = new PricingPlan
        {
            Name = "SAMPLE — درس واحد خاص (كل المراحل)", ServiceTypeId = oneLesson.Id, Mode = SessionMode.Private,
            EffectiveFromUtc = nowUtc.AddDays(-1),
        };
        var privateVersion = new PricingPlanVersion { PlanId = privatePlan.Id, VersionNo = 1, PrivatePrice = 150m, IsLocked = true };
        privatePlan.Versions.Add(privateVersion);
        db.PricingPlans.AddRange(groupPlan, privatePlan);

        // ----- Sample teacher (fictional placeholder) -----
        var teacher = new Teacher
        {
            DisplayNameAr = "مدرس تجريبي (بيانات تجريبية)",
            BioAr = "حساب تجريبي للتطوير فقط. يتم استبداله ببيانات مدرس حقيقي من لوحة الإدارة.",
            QualificationsAr = "—",
            IsPublished = true,
        };
        db.Teachers.Add(teacher);

        // ----- Payment methods (accounts are placeholders the admin must replace) -----
        db.PaymentMethods.AddRange(
            new PaymentMethod { Code = "instapay", NameAr = "إنستاباي", ReceivingAccount = "SAMPLE@instapay — يحدده الأدمن", InstructionsAr = "حوّل المبلغ واكتب رقم الحجز في الملاحظات، وبعدين ارفع صورة التحويل.", SortOrder = 1 },
            new PaymentMethod { Code = "vodafone_cash", NameAr = "فودافون كاش", ReceivingAccount = "010XXXXXXXX — يحدده الأدمن", InstructionsAr = "حوّل المبلغ وبعدين ارفع صورة رسالة التأكيد.", SortOrder = 2 });

        // ----- Sample published sessions -----
        var sec2 = grades.Single(g => g.SortOrder == 21);
        var prep1 = grades.Single(g => g.SortOrder == 10);
        var day = nowUtc.Date.AddDays(3).AddHours(16); // 18:00 Cairo
        db.Sessions.AddRange(
            NewSession("التفاضل والتكامل — قواعد الاشتقاق", sec2, langTrack, math, oneLesson, teacher, day, 90, SessionMode.Group, groupVersion, tierMedium),
            NewSession("قوانين نيوتن للحركة", sec2, arabicTrack, physics, oneLesson, teacher, day.AddDays(1), 60, SessionMode.Group, groupVersion, tierSmall),
            NewSession("المعادلات من الدرجة الأولى", prep1, arabicTrack, math, oneLesson, teacher, day.AddDays(2), 60, SessionMode.Private, privateVersion, null));

        await db.SaveChangesAsync();
    }

    private static Session NewSession(
        string title, Grade grade, StudyTrack track, Subject subject, ServiceType service, Teacher teacher,
        DateTime start, int minutes, SessionMode mode, PricingPlanVersion version, PricingTier? tier) => new()
    {
        ScopeTitleAr = title,
        GradeId = grade.Id, StudyTrackId = track.Id, SubjectId = subject.Id, ServiceTypeId = service.Id, TeacherId = teacher.Id,
        StartUtc = start, EndUtc = start.AddMinutes(minutes), Mode = mode,
        Capacity = tier?.MaxParticipants ?? 1, MinParticipants = tier?.MinParticipants ?? 1,
        PricingPlanVersionId = version.Id, TargetTierId = tier?.Id, Policy = PricingPolicy.TargetTierWithTopUp,
        BookingDeadlineUtc = mode == SessionMode.Group ? start.AddHours(-24) : start.AddHours(-2),
        TierDecisionAtUtc = mode == SessionMode.Group ? start.AddHours(-24) : null,
        Status = SessionStatus.Published,
        MeetingProvider = "GoogleMeet",
        MeetingUrl = "https://meet.google.com/sample-dev-only",
    };
}
