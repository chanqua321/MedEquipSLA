using System.Net;
using MedEquipSLA.Maintenance.Application.Features.WorkOrders;
using Microsoft.Extensions.Configuration;

namespace MedEquipSLA.Maintenance.Infrastructure.Messaging;

public sealed class TechnicianEligibility(HttpClient client) : ITechnicianEligibility
{
    public async Task<long> FindVersionAsync(Guid technician, string authorization, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/users/technicians/{technician}/eligibility");
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return -1;
            return response.StatusCode == HttpStatusCode.NoContent && response.Headers.TryGetValues("X-Authorization-Version", out var values) &&
                long.TryParse(values.SingleOrDefault(), out var version) && version > 0 ? version : 0;
        }
        catch (HttpRequestException) { return 0; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return 0; }
    }
}
