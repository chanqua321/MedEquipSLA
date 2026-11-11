using MedEquipSLA.Notification.Domain;
using MedEquipSLA.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MedEquipSLA.Database.IntegrationTests;

public sealed class PersonalNotificationTests
{
    [SqlServerFact]
    public async Task Only_recipient_can_list_and_mark_read_and_retry_preserves_timestamp()
    {
        await using var database = await TestDatabase.CreateAsync("Notification");
        await using var db = (NotificationDbContext)Contexts.Create("Notification", database.ConnectionString);
        await db.Database.MigrateAsync();
        var recipient = Guid.NewGuid();
        var other = Guid.NewGuid();
        NotificationRecord For(Guid user) => new(user, Guid.NewGuid(), "IncidentResponseSlaBreached",
            Guid.NewGuid(), Guid.NewGuid(), "10:00Z", DateTimeOffset.UtcNow);
        var own = For(recipient);
        var foreign = For(other);
        db.Notifications.AddRange(own, foreign);
        await db.SaveChangesAsync();

        var store = new PersonalNotificationStore(db);
        var unread = await store.ListAsync(recipient, true, 1, default);
        Assert.Equal(own.Id, Assert.Single(unread.Items).Id);
        Assert.False(await store.MarkReadAsync(recipient, foreign.Id, default));
        Assert.True(await store.MarkReadAsync(recipient, own.Id, default));
        Assert.Empty((await store.ListAsync(recipient, true, 1, default)).Items);
        var read = Assert.Single((await store.ListAsync(recipient, false, 1, default)).Items);
        Assert.NotNull(read.ReadAtUtc);
        Assert.True(await store.MarkReadAsync(recipient, own.Id, default));
        Assert.Equal(read.ReadAtUtc, Assert.Single((await store.ListAsync(recipient, false, 1, default)).Items).ReadAtUtc);
        Assert.Equal(foreign.Id, Assert.Single((await store.ListAsync(other, true, 1, default)).Items).Id);
    }
}
