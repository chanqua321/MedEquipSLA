namespace MedEquipSLA.Maintenance.Application.Features.Schedules;

public interface IScheduleTermination
{
    Task<ScheduleResult> ExecuteAsync(Guid id, Guid actor, Guid command, byte[] rowVersion, string reason, bool retryRelease, CancellationToken ct);
    Task<int> ReleaseBatchAsync(CancellationToken ct);
}
