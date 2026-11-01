using Dose.Application.Abstractions;
using Dose.Contracts.Admin;
using Dose.Domain.Bookings;
using Dose.Domain.Catalog;
using Dose.Domain.Common;
using Dose.Domain.Teachers;
using Microsoft.EntityFrameworkCore;

namespace Dose.Application.Administration;

/// <summary>
/// Admin management of catalog data, teachers and payment methods. Records are never hard-deleted:
/// they are deactivated so history (sessions, bookings) stays intact. Every change is audited.
/// </summary>
public sealed class AdminCatalogService(IDoseDbContext db, IClock clock, IFileStorage files)
{
    // ---------- Stages ----------
    public async Task<IReadOnlyList<StageAdminDto>> StagesAsync(CancellationToken ct) =>
        await db.EducationalStages.AsNoTracking().OrderBy(x => x.SortOrder)
            .Select(x => new StageAdminDto(x.Id, x.NameAr, x.RequiresGuardianContact, x.SortOrder, x.IsActive, x.NameEn)).ToListAsync(ct);

    public async Task<Guid> SaveStageAsync(StageAdminDto dto, string actor, CancellationToken ct)
    {
        var e = dto.Id is { } id
            ? AdminGuard.Found(await db.EducationalStages.FindAsync([id], ct), Fields.Stage)
            : new EducationalStage { NameAr = "" };
        e.NameAr = AdminGuard.Name(dto.NameAr);
        e.NameEn = AdminGuard.Optional(dto.NameEn, 200);
        e.RequiresGuardianContact = dto.RequiresGuardianContact;
        e.SortOrder = dto.SortOrder;
        e.IsActive = dto.IsActive;
        if (dto.Id is null) db.EducationalStages.Add(e);
        db.Audit(clock, actor, nameof(EducationalStage), e.Id, dto.Id is null ? "Create" : "Update", dto);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    // ---------- Study tracks ----------
    public async Task<IReadOnlyList<LookupAdminDto>> StudyTracksAsync(CancellationToken ct) =>
        await db.StudyTracks.AsNoTracking().OrderBy(x => x.SortOrder)
            .Select(x => new LookupAdminDto(x.Id, x.NameAr, x.SortOrder, x.IsActive, x.NameEn)).ToListAsync(ct);

    public async Task<Guid> SaveStudyTrackAsync(LookupAdminDto dto, string actor, CancellationToken ct)
    {
        var e = dto.Id is { } id
            ? AdminGuard.Found(await db.StudyTracks.FindAsync([id], ct), Fields.StudyTrack)
            : new StudyTrack { NameAr = "" };
        e.NameAr = AdminGuard.Name(dto.NameAr);
        e.NameEn = AdminGuard.Optional(dto.NameEn, 200);
        e.SortOrder = dto.SortOrder;
        e.IsActive = dto.IsActive;
        if (dto.Id is null) db.StudyTracks.Add(e);
        db.Audit(clock, actor, nameof(StudyTrack), e.Id, dto.Id is null ? "Create" : "Update", dto);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    // ---------- Pricing bands ----------
    public async Task<IReadOnlyList<PricingBandAdminDto>> PricingBandsAsync(CancellationToken ct) =>
        await db.PricingBands.AsNoTracking().OrderBy(x => x.SortOrder)
            .Select(x => new PricingBandAdminDto(x.Id, x.NameAr, x.SortOrder, x.NameEn)).ToListAsync(ct);

    public async Task<Guid> SavePricingBandAsync(PricingBandAdminDto dto, string actor, CancellationToken ct)
    {
        var e = dto.Id is { } id
            ? AdminGuard.Found(await db.PricingBands.FindAsync([id], ct), Fields.PricingBand)
            : new PricingBand { NameAr = "" };
        e.NameAr = AdminGuard.Name(dto.NameAr);
        e.NameEn = AdminGuard.Optional(dto.NameEn, 200);
        e.SortOrder = dto.SortOrder;
        if (dto.Id is null) db.PricingBands.Add(e);
        db.Audit(clock, actor, nameof(PricingBand), e.Id, dto.Id is null ? "Create" : "Update", dto);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    // ---------- Subjects ----------
    public async Task<IReadOnlyList<LookupAdminDto>> SubjectsAsync(CancellationToken ct) =>
        await db.Subjects.AsNoTracking().OrderBy(x => x.SortOrder)
            .Select(x => new LookupAdminDto(x.Id, x.NameAr, x.SortOrder, x.IsActive, x.NameEn)).ToListAsync(ct);

    public async Task<Guid> SaveSubjectAsync(LookupAdminDto dto, string actor, CancellationToken ct)
    {
        var e = dto.Id is { } id
            ? AdminGuard.Found(await db.Subjects.FindAsync([id], ct), Fields.Subject)
            : new Subject { NameAr = "" };
        e.NameAr = AdminGuard.Name(dto.NameAr);
        e.NameEn = AdminGuard.Optional(dto.NameEn, 200);
        e.SortOrder = dto.SortOrder;
        e.IsActive = dto.IsActive;
        if (dto.Id is null) db.Subjects.Add(e);
        db.Audit(clock, actor, nameof(Subject), e.Id, dto.Id is null ? "Create" : "Update", dto);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    // ---------- Grades (with the subjects taught in each grade) ----------
    public async Task<IReadOnlyList<GradeAdminDto>> GradesAsync(CancellationToken ct)
    {
        var grades = await db.Grades.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync(ct);
        var links = await db.GradeSubjects.AsNoTracking().ToListAsync(ct);
        return grades.Select(g => new GradeAdminDto(g.Id, g.NameAr, g.StageId, g.PricingBandId, g.CurriculumSystem, g.SortOrder,
            g.IsActive, links.Where(l => l.GradeId == g.Id).Select(l => l.SubjectId).ToList(), g.NameEn)).ToList();
    }

    public async Task<Guid> SaveGradeAsync(GradeAdminDto dto, string actor, CancellationToken ct)
    {
        if (!await db.EducationalStages.AnyAsync(s => s.Id == dto.StageId, ct))
            throw new DomainException(AdminErrors.Invalid, "اختار المرحلة.", "Choose the stage.");
        if (!await db.PricingBands.AnyAsync(b => b.Id == dto.PricingBandId, ct))
            throw new DomainException(AdminErrors.Invalid, "اختار شريحة التسعير.", "Choose the pricing band.");
        var subjectIds = (dto.SubjectIds ?? []).Distinct().ToList();
        if (await db.Subjects.CountAsync(s => subjectIds.Contains(s.Id), ct) != subjectIds.Count)
            throw new DomainException(AdminErrors.Invalid, "فيه مادة مش موجودة.", "A subject was not found.");

        var e = dto.Id is { } id
            ? AdminGuard.Found(await db.Grades.FindAsync([id], ct), Fields.Grade)
            : new Grade { NameAr = "" };
        e.NameAr = AdminGuard.Name(dto.NameAr);
        e.NameEn = AdminGuard.Optional(dto.NameEn, 200);
        e.StageId = dto.StageId;
        e.PricingBandId = dto.PricingBandId;
        e.CurriculumSystem = AdminGuard.Optional(dto.CurriculumSystem, 100);
        e.SortOrder = dto.SortOrder;
        e.IsActive = dto.IsActive;

        var existing = await db.GradeSubjects.Where(l => l.GradeId == e.Id).ToListAsync(ct);
        db.GradeSubjects.RemoveRange(existing.Where(l => !subjectIds.Contains(l.SubjectId)));
        foreach (var sid in subjectIds.Where(s => existing.All(l => l.SubjectId != s)))
            db.GradeSubjects.Add(new GradeSubject { GradeId = e.Id, SubjectId = sid });

        if (dto.Id is null) db.Grades.Add(e);
        db.Audit(clock, actor, nameof(Grade), e.Id, dto.Id is null ? "Create" : "Update", dto);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    // ---------- Chapters & lessons ----------
    public async Task<IReadOnlyList<ChapterAdminDto>> ChaptersAsync(Guid? gradeId, Guid? subjectId, CancellationToken ct)
    {
        var q = db.Chapters.AsNoTracking();
        if (gradeId is { } g) q = q.Where(c => c.GradeId == g);
        if (subjectId is { } s) q = q.Where(c => c.SubjectId == s);
        return await q.OrderBy(c => c.SortOrder)
            .Select(c => new ChapterAdminDto(c.Id, c.NameAr, c.SubjectId, c.GradeId, c.StudyTrackId, c.SortOrder, c.IsActive, c.NameEn)).ToListAsync(ct);
    }

    public async Task<Guid> SaveChapterAsync(ChapterAdminDto dto, string actor, CancellationToken ct)
    {
        if (!await db.Grades.AnyAsync(g => g.Id == dto.GradeId, ct) || !await db.Subjects.AnyAsync(s => s.Id == dto.SubjectId, ct))
            throw new DomainException(AdminErrors.Invalid, "اختار الصف والمادة.", "Choose the grade and subject.");
        var e = dto.Id is { } id
            ? AdminGuard.Found(await db.Chapters.FindAsync([id], ct), Fields.Chapter)
            : new Chapter { NameAr = "" };
        e.NameAr = AdminGuard.Name(dto.NameAr, Fields.ChapterName, 200);
        e.NameEn = AdminGuard.Optional(dto.NameEn, 200);
        e.GradeId = dto.GradeId;
        e.SubjectId = dto.SubjectId;
        e.StudyTrackId = dto.StudyTrackId;
        e.SortOrder = dto.SortOrder;
        e.IsActive = dto.IsActive;
        if (dto.Id is null) db.Chapters.Add(e);
        db.Audit(clock, actor, nameof(Chapter), e.Id, dto.Id is null ? "Create" : "Update", dto);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    public async Task<IReadOnlyList<LessonAdminDto>> LessonsAsync(Guid chapterId, CancellationToken ct) =>
        await db.Lessons.AsNoTracking().Where(l => l.ChapterId == chapterId).OrderBy(l => l.SortOrder)
            .Select(l => new LessonAdminDto(l.Id, l.NameAr, l.ChapterId, l.SortOrder, l.IsActive, l.NameEn)).ToListAsync(ct);

    public async Task<Guid> SaveLessonAsync(LessonAdminDto dto, string actor, CancellationToken ct)
    {
        if (!await db.Chapters.AnyAsync(c => c.Id == dto.ChapterId, ct))
            throw new DomainException(AdminErrors.Invalid, "اختار الوحدة.", "Choose the chapter.");
        var e = dto.Id is { } id
            ? AdminGuard.Found(await db.Lessons.FindAsync([id], ct), Fields.Lesson)
            : new Lesson { NameAr = "" };
        e.NameAr = AdminGuard.Name(dto.NameAr, Fields.LessonName, 200);
        e.NameEn = AdminGuard.Optional(dto.NameEn, 200);
        e.ChapterId = dto.ChapterId;
        e.SortOrder = dto.SortOrder;
        e.IsActive = dto.IsActive;
        if (dto.Id is null) db.Lessons.Add(e);
        db.Audit(clock, actor, nameof(Lesson), e.Id, dto.Id is null ? "Create" : "Update", dto);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    // ---------- Service types (codes are fixed; names/descriptions/activation are editable) ----------
    public async Task<IReadOnlyList<ServiceTypeAdminDto>> ServiceTypesAsync(CancellationToken ct) =>
        await db.ServiceTypes.AsNoTracking().OrderBy(x => x.SortOrder)
            .Select(x => new ServiceTypeAdminDto(x.Id, x.Code, x.NameAr, x.DescriptionAr, x.SortOrder, x.IsActive, x.NameEn, x.DescriptionEn)).ToListAsync(ct);

    public async Task SaveServiceTypeAsync(ServiceTypeAdminDto dto, string actor, CancellationToken ct)
    {
        var e = AdminGuard.Found(await db.ServiceTypes.FindAsync([dto.Id], ct), Fields.ServiceType);
        e.NameAr = AdminGuard.Name(dto.NameAr);
        e.NameEn = AdminGuard.Optional(dto.NameEn, 200);
        e.DescriptionAr = AdminGuard.Optional(dto.DescriptionAr, 1000);
        e.DescriptionEn = AdminGuard.Optional(dto.DescriptionEn, 1000);
        e.SortOrder = dto.SortOrder;
        e.IsActive = dto.IsActive;
        db.Audit(clock, actor, nameof(ServiceType), e.Id, "Update", dto);
        await db.SaveChangesAsync(ct);
    }

    // ---------- Teachers ----------
    public async Task<IReadOnlyList<TeacherAdminDto>> TeachersAsync(CancellationToken ct) =>
        (await db.Teachers.AsNoTracking().OrderBy(x => x.DisplayNameAr).ToListAsync(ct))
            .Select(x => new TeacherAdminDto(x.Id, x.DisplayNameAr, x.PhotoUrl, x.BioAr, x.QualificationsAr, x.YearsOfExperience, x.IsPublished,
                x.DisplayNameEn, x.BioEn, x.QualificationsEn, x.PhotoFileKey != null, Sessions.SessionQueries.PhotoUrl(x)))
            .ToList();

    public async Task<Guid> SaveTeacherAsync(TeacherAdminDto dto, string actor, CancellationToken ct)
    {
        if (dto.YearsOfExperience is < 0 or > 60) throw new DomainException(AdminErrors.Invalid, "سنين الخبرة مش منطقية.", "Years of experience doesn't look right.");
        var e = dto.Id is { } id
            ? AdminGuard.Found(await db.Teachers.FindAsync([id], ct), Fields.Teacher)
            : new Teacher { DisplayNameAr = "" };
        e.DisplayNameAr = AdminGuard.Name(dto.DisplayNameAr, Fields.TeacherName);
        e.DisplayNameEn = AdminGuard.Optional(dto.DisplayNameEn, 100);
        e.PhotoUrl = AdminGuard.Url(dto.PhotoUrl);
        e.BioAr = AdminGuard.Optional(dto.BioAr, 2000);
        e.QualificationsAr = AdminGuard.Optional(dto.QualificationsAr, 2000);
        e.BioEn = AdminGuard.Optional(dto.BioEn, 2000);
        e.QualificationsEn = AdminGuard.Optional(dto.QualificationsEn, 2000);
        e.YearsOfExperience = dto.YearsOfExperience;
        e.IsPublished = dto.IsPublished;
        if (dto.Id is null) db.Teachers.Add(e);
        db.Audit(clock, actor, nameof(Teacher), e.Id, dto.Id is null ? "Create" : "Update", dto);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    // ---------- Payment methods ----------
    public async Task<IReadOnlyList<PaymentMethodAdminDto>> PaymentMethodsAsync(CancellationToken ct) =>
        await db.PaymentMethods.AsNoTracking().OrderBy(x => x.SortOrder)
            .Select(x => new PaymentMethodAdminDto(x.Id, x.Code, x.NameAr, x.ReceivingAccount, x.InstructionsAr, x.SortOrder, x.IsActive, x.NameEn, x.InstructionsEn))
            .ToListAsync(ct);

    public async Task<Guid> SavePaymentMethodAsync(PaymentMethodAdminDto dto, string actor, CancellationToken ct)
    {
        var code = (dto.Code ?? "").Trim().ToLowerInvariant();
        if (code.Length is < 2 or > 50 || !code.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            throw new DomainException(AdminErrors.Invalid, "الكود لازم يكون حروف إنجليزي صغيرة وأرقام و _ فقط.", "The code may only contain lowercase letters, digits and _.");
        if (await db.PaymentMethods.AnyAsync(m => m.Code == code && m.Id != dto.Id, ct))
            throw new DomainException(AdminErrors.Conflict, "الكود ده مستخدم قبل كده.", "This code is already in use.");

        var e = dto.Id is { } id
            ? AdminGuard.Found(await db.PaymentMethods.FindAsync([id], ct), Fields.PaymentMethod)
            : new PaymentMethod { Code = code, NameAr = "", ReceivingAccount = "" };
        e.Code = code;
        e.NameAr = AdminGuard.Name(dto.NameAr);
        e.NameEn = AdminGuard.Optional(dto.NameEn, 200);
        e.ReceivingAccount = AdminGuard.Name(dto.ReceivingAccount, Fields.ReceivingAccount);
        e.InstructionsAr = AdminGuard.Optional(dto.InstructionsAr, 1000);
        e.InstructionsEn = AdminGuard.Optional(dto.InstructionsEn, 1000);
        e.SortOrder = dto.SortOrder;
        e.IsActive = dto.IsActive;
        if (dto.Id is null) db.PaymentMethods.Add(e);
        db.Audit(clock, actor, nameof(PaymentMethod), e.Id, dto.Id is null ? "Create" : "Update", dto);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    // ---------- Teacher photo ----------

    public const int MaxPhotoKb = 2048;

    /// <summary>Stores an uploaded JPG/PNG/WebP (checked by content) as the teacher's photo.</summary>
    public async Task UploadTeacherPhotoAsync(Guid teacherId, Stream content, long length, string actor, CancellationToken ct)
    {
        var teacher = AdminGuard.Found(await db.Teachers.FirstOrDefaultAsync(t => t.Id == teacherId, ct), Fields.Teacher);
        if (length <= 0 || length > MaxPhotoKb * 1024L)
            throw new DomainException(AdminErrors.Invalid, "الصورة لازم تكون أصغر من 2 ميجا.", "The photo must be smaller than 2 MB.");
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var kind = Bookings.ImageSniffer.Detect(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 16)))
                   ?? throw new DomainException(AdminErrors.Invalid, "ارفع صورة بصيغة JPG أو PNG أو WebP.", "Only JPG, PNG or WebP images are accepted.");
        buffer.Position = 0;
        teacher.PhotoFileKey = await files.SaveAsync(buffer, kind.Extension, ct);
        teacher.PhotoContentType = kind.ContentType;
        teacher.PhotoVersion++;
        db.Audit(clock, actor, nameof(Teacher), teacherId, "UploadPhoto");
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveTeacherPhotoAsync(Guid teacherId, string actor, CancellationToken ct)
    {
        var teacher = AdminGuard.Found(await db.Teachers.FirstOrDefaultAsync(t => t.Id == teacherId, ct), Fields.Teacher);
        teacher.PhotoFileKey = null;
        teacher.PhotoContentType = null;
        teacher.PhotoVersion++;
        db.Audit(clock, actor, nameof(Teacher), teacherId, "RemovePhoto");
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Public: photos of published teachers only.</summary>
    public async Task<(Stream Content, string ContentType)?> OpenTeacherPhotoAsync(Guid teacherId, CancellationToken ct)
    {
        var t = await db.Teachers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == teacherId && x.IsPublished && x.PhotoFileKey != null, ct);
        if (t is null) return null;
        var stream = await files.OpenAsync(t.PhotoFileKey!, ct);
        return stream is null ? null : (stream, t.PhotoContentType ?? "image/jpeg");
    }
}
