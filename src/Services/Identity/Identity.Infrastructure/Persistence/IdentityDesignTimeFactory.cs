using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MedEquipSLA.Identity.Infrastructure.Persistence;

public sealed class IdentityDesignTimeFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__Database");
        var connection = string.IsNullOrWhiteSpace(configured)
            ? "Server=localhost,14333;Database=MedEquip_Identity;Integrated Security=True;Encrypt=True;TrustServerCertificate=True" : configured;
        var parsed = new SqlConnectionStringBuilder(connection);
        if (parsed.InitialCatalog != "MedEquip_Identity" && !parsed.InitialCatalog.StartsWith("MedEquip_Identity_Test", StringComparison.Ordinal))
            throw new InvalidOperationException("Migration connection targets the wrong service database.");
        return new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer(connection, sql => sql.UseCompatibilityLevel(160)).Options);
    }
}
