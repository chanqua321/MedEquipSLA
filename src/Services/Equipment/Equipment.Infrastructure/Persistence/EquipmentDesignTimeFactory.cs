using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MedEquipSLA.Equipment.Infrastructure.Persistence;

public sealed class EquipmentDesignTimeFactory : IDesignTimeDbContextFactory<EquipmentDbContext>
{
    public EquipmentDbContext CreateDbContext(string[] args)
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__Database");
        var connection = string.IsNullOrWhiteSpace(configured)
            ? "Server=localhost,14333;Database=MedEquip_Equipment;Integrated Security=True;Encrypt=True;TrustServerCertificate=True" : configured;
        var parsed = new SqlConnectionStringBuilder(connection);
        if (parsed.InitialCatalog != "MedEquip_Equipment" && !parsed.InitialCatalog.StartsWith("MedEquip_Equipment_Test", StringComparison.Ordinal))
            throw new InvalidOperationException("Migration connection targets the wrong service database.");
        return new EquipmentDbContext(new DbContextOptionsBuilder<EquipmentDbContext>()
            .UseSqlServer(connection, sql => sql.UseCompatibilityLevel(160)).Options);
    }
}
