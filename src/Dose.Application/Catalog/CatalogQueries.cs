using Dose.Application.Abstractions;
using Dose.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Dose.Application.Catalog;

public sealed class CatalogQueries(IDoseDbContext db)
{
    public async Task<IReadOnlyList<StageDto>> StagesWithGradesAsync(CancellationToken ct)
    {
        var stages = await db.EducationalStages.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync(ct);
        var grades = await db.Grades.AsNoTracking().Where(g => g.IsActive).OrderBy(g => g.SortOrder).ToListAsync(ct);
        return stages.Select(s => new StageDto(s.Id, s.NameAr, s.RequiresGuardianContact,
            grades.Where(g => g.StageId == s.Id)
                .Select(g => new GradeDto(g.Id, g.NameAr, s.Id, s.NameAr, s.RequiresGuardianContact)).ToList())).ToList();
    }

    public async Task<IReadOnlyList<LookupDto>> StudyTracksAsync(CancellationToken ct) =>
        await db.StudyTracks.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.SortOrder)
            .Select(t => new LookupDto(t.Id, t.NameAr)).ToListAsync(ct);

    public async Task<IReadOnlyList<LookupDto>> SubjectsAsync(Guid? gradeId, CancellationToken ct)
    {
        var q = db.Subjects.AsNoTracking().Where(s => s.IsActive);
        if (gradeId is { } g)
            q = q.Where(s => db.GradeSubjects.Any(gs => gs.GradeId == g && gs.SubjectId == s.Id));
        return await q.OrderBy(s => s.SortOrder).Select(s => new LookupDto(s.Id, s.NameAr)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ServiceTypeDto>> ServiceTypesAsync(CancellationToken ct) =>
        await db.ServiceTypes.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.SortOrder)
            .Select(s => new ServiceTypeDto(s.Id, s.Code, s.NameAr, s.DescriptionAr)).ToListAsync(ct);

    public async Task<IReadOnlyList<PaymentMethodDto>> PaymentMethodsAsync(CancellationToken ct) =>
        await db.PaymentMethods.AsNoTracking().Where(m => m.IsActive).OrderBy(m => m.SortOrder)
            .Select(m => new PaymentMethodDto(m.Id, m.Code, m.NameAr, m.ReceivingAccount, m.InstructionsAr)).ToListAsync(ct);
}
