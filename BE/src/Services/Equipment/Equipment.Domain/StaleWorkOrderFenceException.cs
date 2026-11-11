namespace MedEquipSLA.Equipment.Domain;

public sealed class StaleWorkOrderFenceException() : InvalidOperationException("Work-order fencing token is stale.");
