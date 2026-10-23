using MedEquipSLA.Application;
using MedEquipSLA.Notification.Application.Abstractions;
namespace MedEquipSLA.Notification.Application.Features.ScaffoldQuery;
public sealed record ScaffoldQuery(Guid Id) : IQuery<bool>;
public sealed class ScaffoldQueryHandler(INotificationQueryAdapter adapter) : IQueryHandler<ScaffoldQuery,bool>
{
    public Task<bool> Handle(ScaffoldQuery request, CancellationToken cancellationToken)
    {
        _ = adapter; _ = request; _ = cancellationToken;
        throw new NotImplementedException("Business use case intentionally deferred; see PRN232-CHECKLIST.md.");
    }
}
