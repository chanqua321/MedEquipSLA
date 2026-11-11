using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MedEquipSLA.Equipment.Api.Authentication;

public sealed class EquipmentServiceAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "EquipmentService";
    public const string WorkOrderPolicy = "MaintenanceWorkOrders";
    public const string Header = "x-maintenance-workorder-key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expected = configuration["Grpc:MaintenanceWorkOrderKey"];
        var supplied = Request.Headers[Header];
        if (string.IsNullOrWhiteSpace(expected) || supplied.Count != 1 || string.IsNullOrEmpty(supplied[0]) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied[0]!)))
            return Task.FromResult(AuthenticateResult.Fail("Maintenance service credentials are required."));
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "Maintenance"),
            new Claim("service", "Maintenance"), new Claim("scope", "equipment.workorders")], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

public static class EquipmentServiceAuthenticationSetup
{
    public static IServiceCollection AddEquipmentServiceAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, EquipmentServiceAuthentication>(
            EquipmentServiceAuthentication.SchemeName, _ => { });
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, RetirementSourceAuthentication>(
            RetirementSourceAuthentication.SchemeName, _ => { });
        services.AddAuthorization(options => options.AddPolicy(RetirementSourceAuthentication.Policy, policy =>
            policy.AddAuthenticationSchemes(RetirementSourceAuthentication.SchemeName).RequireAuthenticatedUser()
                .RequireClaim("service", "Maintenance", "Incident").RequireClaim("scope", "equipment.retirement")));
        services.AddAuthorization(options => options.AddPolicy(EquipmentServiceAuthentication.WorkOrderPolicy, policy =>
            policy.AddAuthenticationSchemes(EquipmentServiceAuthentication.SchemeName)
                .RequireAuthenticatedUser().RequireClaim("service", "Maintenance")
                .RequireClaim("scope", "equipment.workorders")));
        return services;
    }
}
