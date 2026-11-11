using MedEquipSLA.Identity.Application.Abstractions;

namespace MedEquipSLA.Identity.Api;

internal static class MfaEndpoints
{
    internal static void MapMfa(this WebApplication app)
    {
        // Temporarily paused by the owner. Re-enable Authentication:MfaEnabled and restart to map these routes again.
        if (!app.Configuration.GetValue(MedEquipSLA.Identity.Infrastructure.Persistence.MfaSecurity.ConfigurationKey, true)) return;
        // These endpoints authenticate the supplied password and current factor, never a caller-supplied user id.
        var group = app.MapGroup("/api/auth/mfa").WithTags("MFA enrollment and verification").AllowAnonymous();
        group.MapPost("/setup", async (Setup input, HttpContext http, IMfaSecurity mfa, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (!Credentials(input.UserName, input.Password) || input.Code?.Length > 6 || input.RecoveryCode?.Length > 30) return Results.BadRequest();
            return await mfa.SetupAsync(input.UserName!, input.Password!, input.Method ?? "", input.Code, input.RecoveryCode, ct) is { } setup
                ? Results.Ok(setup) : Results.Unauthorized();
        });
        group.MapPost("/confirm", async (Confirm input, HttpContext http, IMfaSecurity mfa, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (input.UserId == Guid.Empty || input.SetupToken?.Length != 64 || input.Code?.Length != 6) return Results.BadRequest();
            return await mfa.ConfirmAsync(input.UserId, input.SetupToken, input.Code, ct) is { } recovery
                ? Results.Ok(new { recoveryCodes = recovery }) : Results.Unauthorized();
        });
        group.MapPost("/email-code", async (Proof input, IMfaSecurity mfa, CancellationToken ct) =>
        {
            if (Credentials(input.UserName, input.Password)) await mfa.EmailCodeAsync(input.UserName!, input.Password!, ct);
            return Results.Accepted(value: new { message = "Nếu yêu cầu hợp lệ, mã sẽ được gửi tới email đã xác nhận." });
        });
        group.MapPost("/disable", async (Proof input, IMfaSecurity mfa, CancellationToken ct) =>
            Credentials(input.UserName, input.Password) && input.Code?.Length is not > 6 && input.RecoveryCode?.Length is not > 30 &&
            await mfa.DisableAsync(input.UserName!, input.Password!, input.Code, input.RecoveryCode, ct) ? Results.NoContent() : Results.Unauthorized());
    }
    private static bool Credentials(string? user, string? password) => user?.Trim().Length is > 0 and <= 256 && password?.Length is > 0 and <= 1024;
    internal sealed record Setup(string? UserName, string? Password, string? Method, string? Code, string? RecoveryCode);
    internal sealed record Confirm(Guid UserId, string? SetupToken, string? Code);
    internal sealed record Proof(string? UserName, string? Password, string? Code, string? RecoveryCode);
}
