namespace MedEquipSLA.Maintenance.Infrastructure.Persistence;

// Selection state only. Durable progress remains on each schedule and its unique occurrences.
public sealed class OccurrencePlanningCursor
{
    private readonly object sync = new();
    private Guid? last;

    public Guid? Read() { lock (sync) return last; }
    public void Advance(Guid? value) { lock (sync) last = value; }
}
