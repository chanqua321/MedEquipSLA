using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MedEquipSLA.Incident.Infrastructure.Persistence;

public sealed class IncidentDesignTimeFactory : IDesignTimeDbContextFactory<IncidentDbContext>
{
    public IncidentDbContext CreateDbContext(string[] args)
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__Database");
        var connection = string.IsNullOrWhiteSpace(configured)
            ? "Server=localhost,14333;Database=MedEquip_Incident;Integrated Security=True;Encrypt=True;TrustServerCertificate=True" : configured;
        var parsed = new SqlConnectionStringBuilder(connection);
        if (parsed.InitialCatalog != "MedEquip_Incident" && !parsed.InitialCatalog.StartsWith("MedEquip_Incident_Test", StringComparison.Ordinal))
            throw new InvalidOperationException("Migration connection targets the wrong service database.");
        return new IncidentDbContext(new DbContextOptionsBuilder<IncidentDbContext>()
            .UseSqlServer(connection, sql => sql.UseCompatibilityLevel(160)).Options);
    }
}
