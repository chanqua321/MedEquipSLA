using Grpc.Core;
using MedEquipSLA.Contracts.Grpc.Equipment.V1;
using MedEquipSLA.Incident.Application.Features.Incidents;
using Microsoft.Extensions.Configuration;

namespace MedEquipSLA.Incident.Infrastructure.Messaging;

public sealed class IncidentEquipmentRegistration(EquipmentLookup.EquipmentLookupClient client, IConfiguration config) : IIncidentEquipmentRegistration
{
    public async Task<EquipmentRegistrationResult> RegisterAsync(Guid equipment, Guid operation, string authorization, CancellationToken ct)
    {
        var key = config["Grpc:IncidentRegistrationKey"];
        if (string.IsNullOrWhiteSpace(key)) return new(IncidentCreationStatus.Unavailable);
        try
        {
            var snapshot = await client.RegisterIncidentUseAsync(new() { EquipmentId = equipment.ToString(), OperationId = operation.ToString() },
                new Metadata { { "authorization", authorization }, { "x-incident-registration-key", key } }, DateTime.UtcNow.AddSeconds(5), ct);
            if (!Guid.TryParse(snapshot.RegistrationId, out var registration) || registration == Guid.Empty ||
                !Guid.TryParse(snapshot.DepartmentId, out var department) || department == Guid.Empty) return new(IncidentCreationStatus.Unavailable);
            return new(IncidentCreationStatus.Created, new(registration, department, snapshot.EquipmentCode, snapshot.EquipmentName, snapshot.DepartmentName));
        }
        catch (RpcException error) when (!ct.IsCancellationRequested)
        { return new(error.StatusCode switch { StatusCode.NotFound => IncidentCreationStatus.NotFound, StatusCode.FailedPrecondition => IncidentCreationStatus.Conflict, _ => IncidentCreationStatus.Unavailable }); }
    }
}
