using MedEquipSLA.Equipment.Domain;
using MedEquipSLA.Equipment.Infrastructure.Persistence;
using MedEquipSLA.Equipment.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class EfPersistenceTests
{
    private static EquipmentDbContext Create(TestDatabase database) => (EquipmentDbContext)Contexts.Create("Equipment", database.ConnectionString);

    // Entry.CurrentValues is fixture setup only. Production transitions still require
    // Domain/Application methods; no public setters or bypass APIs are added.
    private static Department DepartmentFixture(EquipmentDbContext context)
    {
        var department = new Department();
        context.Add(department);
        context.Entry(department).CurrentValues.SetValues(new
        {
            Code = Guid.NewGuid().ToString("N"), Name = "Test department",
            CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        return department;
    }

    private static void OutboxFixture(EquipmentDbContext context, Guid aggregateId, string payload)
    {
        var message = new OutboxMessage();
        context.Add(message);
        context.Entry(message).CurrentValues.SetValues(new
        {
            EventType = "DatabaseProbe", SchemaVersion = 1, AggregateType = "Department",
            AggregateId = aggregateId, OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = "integration-test", PayloadJson = payload, Status = "Pending"
        });
    }

    [SqlServerFact]
    public async Task Ef_throws_concurrency_exception_for_stale_rowversion()
    {
        await using var database = await TestDatabase.CreateAsync("Equipment");
        await using var first = Create(database);
        await first.Database.MigrateAsync();
        var department = DepartmentFixture(first);
        await first.SaveChangesAsync();
        Assert.Equal(8, department.RowVersion.Length);
        await using var second = Create(database);
        var stale = await second.Departments.SingleAsync(x => x.Id == department.Id);
        var oldVersion = stale.RowVersion.ToArray();
        first.Entry(department).Property(x => x.Name).CurrentValue = "First writer";
        await first.SaveChangesAsync();
        Assert.False(oldVersion.SequenceEqual(department.RowVersion));
        second.Entry(stale).Property(x => x.Name).CurrentValue = "Stale writer";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await second.Entry(stale).ReloadAsync();
        Assert.Equal("First writer", stale.Name);
    }

    [SqlServerFact]
    public async Task Ef_transaction_rolls_back_aggregate_and_outbox_together()
    {
        await using var database = await TestDatabase.CreateAsync("Equipment");
        await using (var context = Create(database))
        {
            await context.Database.MigrateAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();
            var department = DepartmentFixture(context);
            OutboxFixture(context, department.Id, "{}");
            await context.SaveChangesAsync();
            Assert.True(await context.Departments.AnyAsync());
            Assert.True(await context.OutboxMessages.AnyAsync());
            await transaction.RollbackAsync();
        }
        await using var verification = Create(database);
        Assert.False(await verification.Departments.AnyAsync());
        Assert.False(await verification.OutboxMessages.AnyAsync());
    }

    [SqlServerFact]
    public async Task Failed_outbox_constraint_rolls_back_other_changes_in_SaveChanges()
    {
        await using var database = await TestDatabase.CreateAsync("Equipment");
        await using (var context = Create(database))
        {
            await context.Database.MigrateAsync();
            var department = DepartmentFixture(context);
            OutboxFixture(context, department.Id, "invalid-json");
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
        await using var verification = Create(database);
        Assert.False(await verification.Departments.AnyAsync());
        Assert.False(await verification.OutboxMessages.AnyAsync());
    }
}
