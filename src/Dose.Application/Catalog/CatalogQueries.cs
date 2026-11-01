using Dose.Application.Abstractions;
using Dose.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Dose.Application.Catalog;

/// <summary>Public catalog. Names follow the request language (English when provided, otherwise Arabic).</summary>
public sealed class CatalogQueries(IDoseDbContext db, IRequestLanguage lang)
{
    public async Task<IReadOnlyList<StageDto>> StagesWithGradesAsync(CancellationToken ct)
    {
        var stages = await db.EducationalStages.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync(ct);
        var grades = await db.Grades.AsNoTracking().Where(g => g.IsActive).OrderBy(g => g.SortOrder).ToListAsync(ct);
        return stages.Select(s => new StageDto(s.Id, lang.Pick(s.NameAr, s.NameEn), s.RequiresGuardianContact,
            grades.Where(g => g.StageId == s.Id)
                .Select(g => new GradeDto(g.Id, lang.Pick(g.NameAr, g.NameEn), s.Id, lang.Pick(s.NameAr, s.NameEn), s.RequiresGuardianContact))
                .ToList())).ToList();
    }

    public async Task<IReadOnlyList<LookupDto>> StudyTracksAsync(CancellationToken ct) =>
        (await db.StudyTracks.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.SortOrder).ToListAsync(ct))
            .Select(t => new LookupDto(t.Id, lang.Pick(t.NameAr, t.NameEn))).ToList();

    public async Task<IReadOnlyList<LookupDto>> SubjectsAsync(Guid? gradeId, CancellationToken ct)
    {
        var q = db.Subjects.AsNoTracking().Where(s => s.IsActive);
        if (gradeId is { } g)
            q = q.Where(s => db.GradeSubjects.Any(gs => gs.GradeId == g && gs.SubjectId == s.Id));
        return (await q.OrderBy(s => s.SortOrder).ToListAsync(ct)).Select(s => new LookupDto(s.Id, lang.Pick(s.NameAr, s.NameEn))).ToList();
    }

    public async Task<IReadOnlyList<ServiceTypeDto>> ServiceTypesAsync(CancellationToken ct) =>
        (await db.ServiceTypes.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync(ct))
            .Select(s => new ServiceTypeDto(s.Id, s.Code, lang.Pick(s.NameAr, s.NameEn),
                s.DescriptionAr is null ? null : lang.Pick(s.DescriptionAr, s.DescriptionEn))).ToList();

    public async Task<IReadOnlyList<PaymentMethodDto>> PaymentMethodsAsync(CancellationToken ct) =>
        (await db.PaymentMethods.AsNoTracking().Where(m => m.IsActive).OrderBy(m => m.SortOrder).ToListAsync(ct))
            .Select(m => new PaymentMethodDto(m.Id, m.Code, lang.Pick(m.NameAr, m.NameEn), m.ReceivingAccount,
                m.InstructionsAr is null ? null : lang.Pick(m.InstructionsAr, m.InstructionsEn))).ToList();
}
