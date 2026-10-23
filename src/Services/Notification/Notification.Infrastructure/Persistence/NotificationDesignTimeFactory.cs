using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MedEquipSLA.Notification.Infrastructure.Persistence;

public sealed class NotificationDesignTimeFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__Database");
        var connection = string.IsNullOrWhiteSpace(configured)
            ? "Server=localhost,14333;Database=MedEquip_Notification;Integrated Security=True;Encrypt=True;TrustServerCertificate=True" : configured;
        var parsed = new SqlConnectionStringBuilder(connection);
        if (parsed.InitialCatalog != "MedEquip_Notification" && !parsed.InitialCatalog.StartsWith("MedEquip_Notification_Test", StringComparison.Ordinal))
            throw new InvalidOperationException("Migration connection targets the wrong service database.");
        return new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>()
            .UseSqlServer(connection, sql => sql.UseCompatibilityLevel(160)).Options);
    }
}
