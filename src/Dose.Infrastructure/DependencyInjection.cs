using Dose.Application.Abstractions;
using Dose.Infrastructure.Persistence;
using Dose.Infrastructure.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dose.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDoseInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Dose");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("Connection string 'Dose' is not configured (ConnectionStrings__Dose).");

        services.AddDbContext<DoseDbContext>(o => o.UseSqlServer(cs));
        services.AddScoped<IDoseDbContext>(sp => sp.GetRequiredService<DoseDbContext>());

        var root = config["Storage:LocalRoot"] ?? Path.Combine(AppContext.BaseDirectory, "private-uploads");
        services.AddSingleton<IFileStorage>(new LocalFileStorage(root));
        // Keys live in the database: login cookies and the encrypted booking links keep working after a restart, redeploy
        // or scale-out. (At rest they are protected by Azure SQL encryption; a Key Vault key can wrap them later.)
        services.AddDataProtection().SetApplicationName("Dose").PersistKeysToDbContext<DoseDbContext>();
        services.AddSingleton<ITokenProtector, Security.DataProtectionTokenProtector>();
        return services;
    }
}
