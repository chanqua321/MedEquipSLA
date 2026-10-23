using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using MedEquipSLA.Contracts.Grpc.Equipment.V1;
namespace MedEquipSLA.Equipment.Api.Grpc;
public sealed class EquipmentGrpcService(IWebHostEnvironment environment) : EquipmentLookup.EquipmentLookupBase
{
    [AllowAnonymous]
    public override Task<ServiceInfoReply> GetServiceInfo(ServiceInfoRequest request, ServerCallContext context)
    {
        if (!environment.IsDevelopment()) throw new RpcException(new Status(StatusCode.PermissionDenied, "Development probe only"));
        return Task.FromResult(new ServiceInfoReply { Service = "Equipment", Phase = "scaffold" });
    }
    [Authorize]
    public override Task<EquipmentReply> GetEquipment(GetEquipmentRequest request, ServerCallContext context) =>
        throw new RpcException(new Status(StatusCode.Unimplemented, "Business lookup deferred until BR approval"));
}
