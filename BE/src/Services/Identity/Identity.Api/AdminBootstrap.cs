using MedEquipSLA.Identity.Application.Abstractions;

namespace MedEquipSLA.Identity.Api;

internal static class AdminBootstrap
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAdminProvisioning>().ProvisionAsync(
            configuration["BootstrapAdmin:UserName"] ?? "", configuration["BootstrapAdmin:Email"] ?? "",
            configuration["BootstrapAdmin:Password"] ?? "", CancellationToken.None);
    }
}
