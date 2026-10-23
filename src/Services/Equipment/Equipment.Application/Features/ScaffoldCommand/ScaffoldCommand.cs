using MedEquipSLA.Application;
using MedEquipSLA.Equipment.Application.Abstractions;
namespace MedEquipSLA.Equipment.Application.Features.ScaffoldCommand;
public sealed record ScaffoldCommand(Guid Id) : ICommand<bool>;
public sealed class ScaffoldCommandHandler(IEquipmentRepository adapter) : ICommandHandler<ScaffoldCommand,bool>
{
    public Task<bool> Handle(ScaffoldCommand request, CancellationToken cancellationToken)
    {
        _ = adapter; _ = request; _ = cancellationToken;
        throw new NotImplementedException("Business use case intentionally deferred; see PRN232-CHECKLIST.md.");
    }
}
