namespace MedEquipSLA.Maintenance.Infrastructure.Persistence;

public sealed class OccurrencePlanningOptions
{
    public int ScheduleBatchSize { get; set; } = 50;
    public int OccurrencesPerSchedule { get; set; } = 4;
}
