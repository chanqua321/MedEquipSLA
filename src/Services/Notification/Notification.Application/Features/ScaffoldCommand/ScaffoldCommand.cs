using MedEquipSLA.Application;
using MedEquipSLA.Notification.Application.Abstractions;
namespace MedEquipSLA.Notification.Application.Features.ScaffoldCommand;
public sealed record ScaffoldCommand(Guid Id) : ICommand<bool>;
public sealed class ScaffoldCommandHandler(INotificationRepository adapter) : ICommandHandler<ScaffoldCommand,bool>
{
    public Task<bool> Handle(ScaffoldCommand request, CancellationToken cancellationToken)
    {
        _ = adapter; _ = request; _ = cancellationToken;
        throw new NotImplementedException("Business use case intentionally deferred; see PRN232-CHECKLIST.md.");
    }
}
