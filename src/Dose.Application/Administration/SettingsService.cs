using System.Globalization;
using Dose.Application.Abstractions;
using Dose.Domain.Administration;
using Dose.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Dose.Application.Administration;

/// <summary>Admin-editable operational settings. Defaults apply only until an admin saves a value.</summary>
public static class SettingKeys
{
    public const string HoldMinutes = "booking.hold_minutes";
    public const string PaymentProofDeadlineMinutes = "booking.payment_proof_deadline_minutes";
    public const string ProofResubmitMinutes = "booking.proof_resubmit_minutes";
    public const string JoinOpensMinutesBeforeStart = "session.join_opens_minutes_before_start";
    public const string TierDecisionHoursBeforeStart = "group.tier_decision_hours_before_start";
    public const string MaxProofFileSizeKb = "payment.max_proof_file_size_kb";
    public const string PrivateBookingDeadlineHours = "session.private_booking_deadline_hours";
    public const string TopUpResponseHours = "group.topup_response_hours";
    public const string FullRefundCancellationHours = "booking.full_refund_cancellation_hours";
    public const string ProofRetentionDays = "privacy.payment_proof_retention_days";
    public const string PrivateMinLeadHours = "private.min_lead_hours";
    public const string OfferHoldHours = "private.offer_hold_hours";

    public static readonly IReadOnlyDictionary<string, (string Default, string DescriptionAr)> Definitions =
        new Dictionary<string, (string, string)>
        {
            [HoldMinutes] = ("15", "مدة حجز المقعد مؤقتاً لحد ما الطالب يكتب بياناته (بالدقائق)"),
            [PaymentProofDeadlineMinutes] = ("60", "المهلة لرفع إيصال الدفع بعد إدخال البيانات (بالدقائق)"),
            [ProofResubmitMinutes] = ("60", "مهلة إعادة رفع الإيصال بعد رفضه (بالدقائق)"),
            [JoinOpensMinutesBeforeStart] = ("15", "زر دخول الجلسة يتفعّل قبل الميعاد بكام دقيقة"),
            [TierDecisionHoursBeforeStart] = ("24", "موعد حسم اكتمال الجروب قبل الجلسة (بالساعات)"),
            [MaxProofFileSizeKb] = ("5120", "أقصى حجم لصورة الإيصال (كيلوبايت)"),
            [PrivateBookingDeadlineHours] = ("2", "آخر ميعاد لحجز الجلسة الفردية قبل بدايتها (بالساعات)"),
            [TopUpResponseHours] = ("6", "مهلة رد الطالب لو الجروب مكملش (بالساعات)"),
            [FullRefundCancellationHours] = ("24", "الطالب يسترد فلوسه كاملة لو لغى قبل الجلسة بكام ساعة"),
            [ProofRetentionDays] = ("180", "صور إيصالات الدفع بتتمسح بعد انتهاء الجلسة بكام يوم"),
            [PrivateMinLeadHours] = ("24", "أقرب ميعاد يقدر الطالب يحجزه بنفسه لجلسة خاصة أو مجموعة خاصة (بعد كام ساعة)"),
            [OfferHoldHours] = ("24", "مهلة الطالب علشان يأكد ويدفع الميعاد اللي الإدارة عرضته عليه (بالساعات)"),
        };
}

public sealed record BookingSettings(
    int HoldMinutes,
    int PaymentProofDeadlineMinutes,
    int ProofResubmitMinutes,
    int JoinOpensMinutesBeforeStart,
    int TierDecisionHoursBeforeStart,
    int MaxProofFileSizeKb,
    int PrivateBookingDeadlineHours,
    int TopUpResponseHours,
    int FullRefundCancellationHours);

public sealed class SettingsService(IDoseDbContext db, IClock clock)
{
    public async Task<BookingSettings> GetBookingSettingsAsync(CancellationToken ct)
    {
        var values = await GetAllAsync(ct);
        int I(string k) => int.Parse(values[k], CultureInfo.InvariantCulture);
        return new BookingSettings(
            I(SettingKeys.HoldMinutes),
            I(SettingKeys.PaymentProofDeadlineMinutes),
            I(SettingKeys.ProofResubmitMinutes),
            I(SettingKeys.JoinOpensMinutesBeforeStart),
            I(SettingKeys.TierDecisionHoursBeforeStart),
            I(SettingKeys.MaxProofFileSizeKb),
            I(SettingKeys.PrivateBookingDeadlineHours),
            I(SettingKeys.TopUpResponseHours),
            I(SettingKeys.FullRefundCancellationHours));
    }

    public async Task<int> GetIntAsync(string key, CancellationToken ct) =>
        int.Parse((await GetAllAsync(ct))[key], CultureInfo.InvariantCulture);

    public async Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct)
    {
        var stored = await db.SystemSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        return SettingKeys.Definitions.ToDictionary(
            d => d.Key,
            d => stored.TryGetValue(d.Key, out var v) ? v : d.Value.Default);
    }

    public async Task UpdateAsync(IReadOnlyDictionary<string, string> updates, string actor, CancellationToken ct)
    {
        foreach (var (key, value) in updates)
        {
            if (!SettingKeys.Definitions.ContainsKey(key))
                throw new DomainException("settings.unknown_key", $"إعداد غير معروف '{key}'.", $"Unknown setting '{key}'.");
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 1 || n > 100_000)
                throw new DomainException("settings.invalid_value", "القيمة لازم تكون رقم صحيح موجب.", $"Setting '{key}' must be a positive whole number.");

            var existing = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
            var old = existing?.Value ?? SettingKeys.Definitions[key].Default;
            if (existing is null)
            {
                db.SystemSettings.Add(new SystemSetting
                {
                    Key = key, Value = n.ToString(CultureInfo.InvariantCulture),
                    DescriptionAr = SettingKeys.Definitions[key].DescriptionAr,
                    UpdatedBy = actor, UpdatedAtUtc = clock.UtcNow,
                });
            }
            else
            {
                existing.Value = n.ToString(CultureInfo.InvariantCulture);
                existing.UpdatedBy = actor;
                existing.UpdatedAtUtc = clock.UtcNow;
            }

            db.AuditLogs.Add(new AuditLog
            {
                Actor = actor, Entity = nameof(SystemSetting), EntityId = key, Action = "Update",
                DataJson = $$"""{"from":"{{old}}","to":"{{n}}"}""", AtUtc = clock.UtcNow,
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
