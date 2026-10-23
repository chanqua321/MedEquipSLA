using MedEquipSLA.Application;
using MedEquipSLA.Equipment.Application.Abstractions;
namespace MedEquipSLA.Equipment.Application.Features.ScaffoldQuery;
public sealed record ScaffoldQuery(Guid Id) : IQuery<bool>;
public sealed class ScaffoldQueryHandler(IEquipmentQueryAdapter adapter) : IQueryHandler<ScaffoldQuery,bool>
{
    public Task<bool> Handle(ScaffoldQuery request, CancellationToken cancellationToken)
    {
        _ = adapter; _ = request; _ = cancellationToken;
        throw new NotImplementedException("Business use case intentionally deferred; see PRN232-CHECKLIST.md.");
    }
}
