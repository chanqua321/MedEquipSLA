using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using MedEquipSLA.Identity.Application.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace MedEquipSLA.Identity.Api;

internal static class SessionEndpoints
{
    public static void MapSessions(this WebApplication app)
    {
        var endpoints = app.MapGroup("/api/auth").WithTags("Authentication");
        endpoints.MapGet("/me", async (ClaimsPrincipal principal, IIdentitySessions sessions, CancellationToken ct) =>
        {
            var claims = SessionClaims.ReadClaims(principal);
            return claims is not null && await sessions.IsCurrentAsync(claims, ct)
                ? Results.Ok(claims) : Results.Unauthorized();
        }).RequireAuthorization();
        endpoints.MapPost("/logout", async (bool? all, ClaimsPrincipal principal, IIdentitySessions sessions, CancellationToken ct) =>
        {
            var claims = SessionClaims.ReadClaims(principal);
            if (claims is null) return Results.Unauthorized();
            await sessions.RevokeAsync(claims, all == true, ct);
            return Results.NoContent();
        }).RequireAuthorization();
        endpoints.MapPost("/login", async (LoginRequest request, IIdentitySessions sessions, AccessTokenIssuer tokens, CancellationToken ct) =>
        {
            if (!tokens.Enabled) return Results.StatusCode(503);
            if (request.UserName?.Trim() is not { Length: > 0 and <= 256 } name ||
                request.Password is not { Length: > 0 and <= 1024 } password)
                return Results.BadRequest();
            if (request.Code?.Length > 6 || request.RecoveryCode?.Length > 30) return Results.BadRequest();
            return Response(await sessions.LoginAsync(name, password, ct, request.Code, request.RecoveryCode), tokens);
        }).AllowAnonymous();

        endpoints.MapPost("/refresh", async (RefreshRequest request, IIdentitySessions sessions, AccessTokenIssuer tokens, CancellationToken ct) =>
        {
            if (!tokens.Enabled) return Results.StatusCode(503);
            if (request.RefreshToken is not { Length: 64 } refreshToken || !refreshToken.All(Uri.IsHexDigit))
                return Results.BadRequest();
            return Response(await sessions.RefreshAsync(refreshToken, ct), tokens);
        }).AllowAnonymous();
    }

    private static IResult Response(SessionGrant? grant, AccessTokenIssuer tokens) =>
        grant is null ? Results.Unauthorized() : Results.Ok(tokens.Issue(grant));

    internal sealed record LoginRequest(string? UserName, string? Password, string? Code = null, string? RecoveryCode = null);
    internal sealed record RefreshRequest(string? RefreshToken);
}

internal sealed class AccessTokenIssuer : IDisposable
{
    private readonly RSA? rsa;
    private readonly SigningCredentials? signing;
    private readonly string? issuer;
    private readonly string? audience;

    public AccessTokenIssuer(IConfiguration configuration)
    {
        var path = configuration["Jwt:PrivateKeyPath"];
        issuer = configuration["Jwt:Issuer"];
        audience = configuration["Jwt:Audience"];
        if (string.IsNullOrWhiteSpace(path))
            return;
        var key = RSA.Create();
        try
        {
            key.ImportFromPem(File.ReadAllText(path));
            if (key.KeySize < 2048 || key.ExportParameters(true).D is null ||
                string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
                throw new InvalidOperationException("JWT signing requires a private RSA key of at least 2048 bits, issuer and audience.");
            rsa = key;
            signing = new(new RsaSecurityKey(key), SecurityAlgorithms.RsaSha256);
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    public bool Enabled => signing is not null;

    public object Issue(SessionGrant grant)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(15);
        var user = grant.User;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("session_id", user.SessionId.ToString()),
            new("authorization_version", user.AuthorizationVersion.ToString(CultureInfo.InvariantCulture))
        };
        if (user.DepartmentId is Guid department)
            claims.Add(new("department_id", department.ToString()));
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var jwt = new JwtSecurityToken(issuer, audience, claims, now.UtcDateTime, expires.UtcDateTime, signing);
        return new
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(jwt),
            AccessExpiresAtUtc = expires,
            grant.RefreshToken,
            grant.RefreshExpiresAtUtc
        };
    }

    public void Dispose() => rsa?.Dispose();
}

internal static class SessionClaims
{
    internal static SessionUser? ReadClaims(ClaimsPrincipal principal)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ||
            !long.TryParse(principal.FindFirstValue("authorization_version"), out var version) ||
            !Guid.TryParse(principal.FindFirstValue("session_id"), out var sessionId))
            return null;
        var departmentClaim = principal.FindFirstValue("department_id");
        Guid? department = null;
        if (departmentClaim is not null)
        {
            if (!Guid.TryParse(departmentClaim, out var parsed))
                return null;
            department = parsed;
        }
        var roles = principal.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();
        return new(id, department, version, roles, sessionId);
    }
}
