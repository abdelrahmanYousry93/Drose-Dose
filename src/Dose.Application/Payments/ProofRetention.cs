using Dose.Application.Abstractions;
using Dose.Application.Administration;
using Dose.Domain.Administration;
using Microsoft.EntityFrameworkCore;

namespace Dose.Application.Payments;

/// <summary>
/// Data retention (decision D12): payment screenshots are deleted a configurable number of days (default 180) after
/// the session ended. Only the image file goes — the payment record (amount, method, reviewer, status) stays for
/// accounting. Idempotent; runs daily.
/// </summary>
public sealed class ProofRetention(IDoseDbContext db, IFileStorage files, SettingsService settings, IClock clock)
{
    public async Task<int> PurgeAsync(CancellationToken ct)
    {
        var days = int.Parse((await settings.GetAllAsync(ct))[SettingKeys.ProofRetentionDays]);
        var now = clock.UtcNow;
        var cutoff = now.AddDays(-days);
        var due = await db.PaymentProofs
            .Where(p => p.FileDeletedAtUtc == null && p.Booking!.Session!.EndUtc <= cutoff)
            .OrderBy(p => p.SubmittedAtUtc)
            .Take(500)
            .ToListAsync(ct);
        if (due.Count == 0) return 0;

        foreach (var proof in due)
        {
            await files.DeleteAsync(proof.FileKey, ct);
            proof.FileDeletedAtUtc = now;
        }
        db.AuditLogs.Add(new AuditLog
        {
            Actor = "system", Entity = "PaymentProof", EntityId = "retention", Action = "DeleteScreenshots",
            DataJson = $$"""{"count":{{due.Count}},"retentionDays":{{days}}}""", AtUtc = now,
        });
        await db.SaveChangesAsync(ct);
        return due.Count;
    }
}
