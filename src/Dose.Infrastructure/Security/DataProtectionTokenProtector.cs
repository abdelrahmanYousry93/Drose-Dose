using System.Security.Cryptography;
using Dose.Application.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace Dose.Infrastructure.Security;

/// <summary>Encrypts booking link tokens with ASP.NET Core Data Protection (keys must persist across deployments).</summary>
public sealed class DataProtectionTokenProtector(IDataProtectionProvider provider) : ITokenProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("Dose.BookingLinks.v1");

    public string Protect(string token) => _protector.Protect(token);

    public string? Unprotect(string protectedToken)
    {
        try { return _protector.Unprotect(protectedToken); }
        catch (CryptographicException) { return null; } // keys rotated/lost: the admin can issue a new link
    }
}
