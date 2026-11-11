using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MedEquipSLA.Identity.Application.Abstractions;

namespace MedEquipSLA.Identity.Api;

internal static class AccountProvisioningEndpoints
{
    internal static void MapAccountProvisioning(this WebApplication app)
    {
        var employees = app.MapGroup("/api/users").WithTags("Employee provisioning").RequireAuthorization(p => p.RequireRole("Admin"));
        employees.AddEndpointFilter(async (context, next) =>
        {
            var actor = SessionClaims.ReadClaims(context.HttpContext.User);
            return actor is not null && await context.HttpContext.RequestServices.GetRequiredService<IIdentitySessions>()
                .IsCurrentAsync(actor, context.HttpContext.RequestAborted) ? await next(context) : Results.Unauthorized();
        });
        employees.MapPost("", async (Invite input, HttpContext http, IAccountProvisioning accounts,
            IHttpClientFactory clients, CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Results.Forbid();
            var department = await DepartmentAsync(input.DepartmentId, http, clients, ct);
            if (input.DepartmentId is not null && department is null) return Results.BadRequest();
            var result = await accounts.InviteAsync(new(input.Email ?? "", input.FullName ?? "", input.Role ?? "", input.DepartmentId, department), actor, ct);
            return Provision(result);
        });
        employees.MapPost("/{id:guid}/activation/resend", async (Guid id, Resend input, HttpContext http, IAccountProvisioning accounts, CancellationToken ct) =>
            Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor) && input.RowVersion is { Length: 8 }
                ? Provision(await accounts.ResendAsync(id, actor, input.RowVersion, ct)) : Results.BadRequest());
        var auth = app.MapGroup("/api/auth").WithTags("Account activation");
        auth.MapPost("/activate", async (Redeem input, IAccountProvisioning accounts, CancellationToken ct) =>
            Valid(input) && await accounts.ActivateAsync(input.UserId, input.TicketId, input.Token!, input.Password!, ct) ? Results.NoContent() : Results.BadRequest()).AllowAnonymous();
        auth.MapPost("/password-reset/request", async (ResetRequest input, IAccountProvisioning accounts, CancellationToken ct) =>
        {
            if (input.Email?.Length is > 0 and <= 256) await accounts.RequestResetAsync(input.Email, ct);
            return Results.Accepted(value: new { message = "Nếu tài khoản phù hợp tồn tại, hệ thống sẽ gửi hướng dẫn." });
        }).AllowAnonymous();
        auth.MapPost("/password-reset/confirm", async (Redeem input, IAccountProvisioning accounts, CancellationToken ct) =>
            Valid(input) && await accounts.ResetAsync(input.UserId, input.TicketId, input.Token!, input.Password!, ct) ? Results.NoContent() : Results.BadRequest()).AllowAnonymous();
        app.MapGet("/api/internal/security-mail/{user:guid}/{ticket:guid}", async (Guid user, Guid ticket, HttpContext http,
            IConfiguration config, IAccountProvisioning accounts, CancellationToken ct) =>
        {
            var expected = config["Identity:RecipientLookupKey"]; var supplied = http.Request.Headers["X-Recipient-Lookup-Key"];
            if (string.IsNullOrWhiteSpace(expected) || supplied.Count != 1 || string.IsNullOrEmpty(supplied[0]) ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied[0]!))) return Results.Unauthorized();
            http.Response.Headers.CacheControl = "no-store";
            return await accounts.MailAsync(user, ticket, ct) is { } mail ? Results.Ok(mail) : Results.NotFound();
        }).AllowAnonymous().WithTags("Internal security mail");
    }
    internal static async Task<string?> DepartmentAsync(Guid? id, HttpContext http, IHttpClientFactory clients, CancellationToken ct)
    {
        if (id is null) return null;
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/departments/{id}");
        request.Headers.TryAddWithoutValidation("Authorization", http.Request.Headers.Authorization.ToString());
        using var response = await clients.CreateClient("EquipmentDirectory").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        var value = await response.Content.ReadFromJsonAsync<Department>(ct);
        return value is { IsActive: true } ? value.Name : null;
    }
    private static bool Valid(Redeem input) => input.UserId != Guid.Empty && input.TicketId != Guid.Empty && input.Token is { Length: 64 } && input.Password?.Length is >= 12 and <= 1024;
    private static IResult Provision(ProvisionResult result) => result.Status switch
    {
        "Created" => Results.Accepted(value: new { result.UserId, status = "PendingActivation" }),
        "NotFound" => Results.NotFound(), "Duplicate" or "Conflict" => Results.Conflict(new { result.UserId, result.Status }), _ => Results.BadRequest()
    };
    internal sealed record Invite(string? Email, string? FullName, string? Role, Guid? DepartmentId);
    internal sealed record Resend(byte[]? RowVersion);
    internal sealed record Redeem(Guid UserId, Guid TicketId, string? Token, string? Password);
    internal sealed record ResetRequest(string? Email);
    private sealed record Department(string Name, bool IsActive);
}
