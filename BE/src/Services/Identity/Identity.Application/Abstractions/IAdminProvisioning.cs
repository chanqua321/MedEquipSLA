namespace MedEquipSLA.Identity.Application.Abstractions;

public interface IAdminProvisioning
{
    Task ProvisionAsync(string userName, string email, string password, CancellationToken ct);
}
