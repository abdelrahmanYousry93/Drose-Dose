using System.Text.Json;
using Dose.Application.Abstractions;
using Dose.Application.Administration;
using Dose.Contracts.Admin;
using Dose.Domain.Administration;
using Dose.Domain.Common;
using Dose.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dose.Infrastructure.Identity;

/// <summary>
/// Admin and teacher accounts. Students never have accounts. Safeguards: an admin can't disable themselves,
/// and the last active admin can't be disabled. Disabling also rotates the security stamp so sessions end.
/// </summary>
public sealed class UserAdminService(UserManager<IdentityUser> users, DoseDbContext db, IClock clock)
{
    public static readonly string[] Roles = [DevelopmentSeeder.AdminRole, DevelopmentSeeder.TeacherRole];

    public async Task<IReadOnlyList<UserAdminDto>> ListAsync(CancellationToken ct)
    {
        var all = await users.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync(ct);
        var roleRows = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id select new { ur.UserId, r.Name })
            .ToListAsync(ct);
        var teachers = await db.Teachers.AsNoTracking().Where(t => t.UserId != null).ToDictionaryAsync(t => t.UserId!, ct);
        var now = DateTimeOffset.UtcNow;
        return all.Select(u =>
        {
            teachers.TryGetValue(u.Id, out var t);
            return new UserAdminDto(u.Id, u.Email ?? u.UserName ?? "", roleRows.Where(r => r.UserId == u.Id).Select(r => r.Name!).ToList(),
                u.LockoutEnd > now.AddYears(10), t?.Id, t?.DisplayNameAr);
        }).ToList();
    }

    public async Task<string> CreateAsync(CreateUserRequest req, string actor, CancellationToken ct)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        if (!System.Net.Mail.MailAddress.TryCreate(email, out _)) throw new DomainException(AdminErrors.Invalid, "الإيميل مش صحيح.");
        if (!Roles.Contains(req.Role)) throw new DomainException(AdminErrors.Invalid, "اختار الدور: أدمن أو مدرس.");
        if (await users.FindByEmailAsync(email) is not null) throw new DomainException(AdminErrors.Conflict, "الإيميل ده عنده حساب بالفعل.");
        Domain.Teachers.Teacher? teacher = null;
        if (req.Role == DevelopmentSeeder.TeacherRole)
        {
            teacher = await db.Teachers.FirstOrDefaultAsync(t => t.Id == req.TeacherId, ct)
                      ?? throw new DomainException(AdminErrors.Invalid, "اختار ملف المدرس اللي الحساب ده بتاعه.");
            if (teacher.UserId is not null) throw new DomainException(AdminErrors.Conflict, "ملف المدرس ده مربوط بحساب تاني.");
        }

        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        Check(await users.CreateAsync(user, req.Password ?? ""));
        Check(await users.AddToRoleAsync(user, req.Role));
        if (teacher is not null) teacher.UserId = user.Id;
        Audit(actor, user.Id, "Create", new { email, req.Role, req.TeacherId });
        await db.SaveChangesAsync(ct);
        return user.Id;
    }

    public async Task ResetPasswordAsync(string userId, string password, string actor, CancellationToken ct)
    {
        var user = await FindAsync(userId);
        var token = await users.GeneratePasswordResetTokenAsync(user);
        Check(await users.ResetPasswordAsync(user, token, password ?? ""));
        await users.ResetAccessFailedCountAsync(user);
        Audit(actor, userId, "ResetPassword");
        await db.SaveChangesAsync(ct);
    }

    public async Task SetDisabledAsync(string userId, bool disabled, string actorUserId, string actor, CancellationToken ct)
    {
        var user = await FindAsync(userId);
        if (disabled && userId == actorUserId) throw new DomainException(AdminErrors.Invalid, "مينفعش توقف حسابك إنت.");
        if (disabled && await users.IsInRoleAsync(user, DevelopmentSeeder.AdminRole))
        {
            var admins = await users.GetUsersInRoleAsync(DevelopmentSeeder.AdminRole);
            var active = admins.Count(a => a.LockoutEnd is null || a.LockoutEnd <= DateTimeOffset.UtcNow);
            if (active <= 1) throw new DomainException(AdminErrors.Invalid, "ده آخر أدمن شغال، مينفعش يتوقف.");
        }
        await users.SetLockoutEnabledAsync(user, true);
        Check(await users.SetLockoutEndDateAsync(user, disabled ? DateTimeOffset.MaxValue : null));
        await users.UpdateSecurityStampAsync(user); // ends existing sessions at the next validation
        Audit(actor, userId, disabled ? "Disable" : "Enable");
        await db.SaveChangesAsync(ct);
    }

    public async Task LinkTeacherAsync(string userId, Guid? teacherId, string actor, CancellationToken ct)
    {
        var user = await FindAsync(userId);
        if (!await users.IsInRoleAsync(user, DevelopmentSeeder.TeacherRole))
            throw new DomainException(AdminErrors.Invalid, "الحساب ده مش حساب مدرس.");
        var current = await db.Teachers.Where(t => t.UserId == userId).ToListAsync(ct);
        foreach (var t in current) t.UserId = null;
        if (teacherId is { } id)
        {
            var teacher = await db.Teachers.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new DomainException(AdminErrors.Invalid, "ملف المدرس مش موجود.");
            if (teacher.UserId is not null && teacher.UserId != userId) throw new DomainException(AdminErrors.Conflict, "ملف المدرس ده مربوط بحساب تاني.");
            teacher.UserId = userId;
        }
        Audit(actor, userId, "LinkTeacher", new { teacherId });
        await db.SaveChangesAsync(ct);
    }

    private async Task<IdentityUser> FindAsync(string userId) =>
        await users.FindByIdAsync(userId) ?? throw new DomainException(AdminErrors.NotFound, "الحساب مش موجود.");

    private void Audit(string actor, string userId, string action, object? data = null) =>
        db.AuditLogs.Add(new AuditLog
        {
            Actor = actor, Entity = "User", EntityId = userId, Action = action,
            DataJson = data is null ? null : JsonSerializer.Serialize(data), AtUtc = clock.UtcNow,
        });

    private static void Check(IdentityResult r)
    {
        if (r.Succeeded) return;
        var msg = string.Join(" ", r.Errors.Select(e => e.Code switch
        {
            "PasswordTooShort" => "كلمة السر لازم تكون 10 حروف على الأقل.",
            "PasswordRequiresDigit" => "كلمة السر لازم فيها رقم.",
            "PasswordRequiresLower" => "كلمة السر لازم فيها حرف إنجليزي صغير.",
            "PasswordRequiresUpper" => "كلمة السر لازم فيها حرف إنجليزي كبير.",
            "PasswordRequiresNonAlphanumeric" => "كلمة السر لازم فيها رمز زي ! أو @.",
            "DuplicateEmail" or "DuplicateUserName" => "الإيميل ده عنده حساب بالفعل.",
            _ => e.Description,
        }).Distinct());
        throw new DomainException(AdminErrors.Invalid, msg);
    }
}
