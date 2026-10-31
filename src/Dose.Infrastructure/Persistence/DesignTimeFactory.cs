using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dose.Infrastructure.Persistence;

/// <summary>Used by `dotnet ef` only. Reads ConnectionStrings__Dose, falling back to local dev SQL Server.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<DoseDbContext>
{
    public DoseDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Dose")
                 ?? "Server=localhost,1433;Database=Dose;Integrated Security=false;TrustServerCertificate=True";
        return new DoseDbContext(new DbContextOptionsBuilder<DoseDbContext>().UseSqlServer(cs).Options);
    }
}
