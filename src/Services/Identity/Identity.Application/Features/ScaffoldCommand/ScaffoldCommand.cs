using MedEquipSLA.Application;
using MedEquipSLA.Identity.Application.Abstractions;
namespace MedEquipSLA.Identity.Application.Features.ScaffoldCommand;
public sealed record ScaffoldCommand(Guid Id) : ICommand<bool>;
public sealed class ScaffoldCommandHandler(IIdentityRepository adapter) : ICommandHandler<ScaffoldCommand,bool>
{
    public Task<bool> Handle(ScaffoldCommand request, CancellationToken cancellationToken)
    {
        _ = adapter; _ = request; _ = cancellationToken;
        throw new NotImplementedException("Business use case intentionally deferred; see PRN232-CHECKLIST.md.");
    }
}
