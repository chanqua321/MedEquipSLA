using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MedEquipSLA.Equipment.Api.Authentication;

public sealed class RetirementSourceAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "RetirementSource";
    public const string Policy = "EquipmentRetirementSources";
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var source = Request.Headers["x-retirement-source"];
        var key = Request.Headers["x-retirement-source-key"];
        if (source.Count != 1 || key.Count != 1 || source[0] is not ("Maintenance" or "Incident"))
            return Task.FromResult(AuthenticateResult.Fail("A bound retirement source credential is required."));
        var expected = configuration[$"Grpc:Retirement{source[0]}Key"];
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrEmpty(key[0]) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(key[0]!)))
            return Task.FromResult(AuthenticateResult.Fail("Invalid retirement source credential."));
        var identity = new ClaimsIdentity([new("service", source[0]!), new("scope", "equipment.retirement")], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
