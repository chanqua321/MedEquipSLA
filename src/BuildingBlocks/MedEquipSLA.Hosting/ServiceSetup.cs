using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
namespace MedEquipSLA.Hosting;
public static class ServiceSetup
{
    public static IServiceCollection AddServiceScaffold(this IServiceCollection services, IConfiguration config)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<ScaffoldExceptionHandler>();
        services.AddEndpointsApiExplorer(); services.AddSwaggerGen();
        services.AddHealthChecks();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            SecurityKey? key = null;
            var path = config["Jwt:PublicKeyPath"];
            if (!string.IsNullOrWhiteSpace(path))
            { var rsa = RSA.Create(); rsa.ImportFromPem(File.ReadAllText(path)); key = new RsaSecurityKey(rsa); }
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = config["Jwt:Issuer"],
                ValidateAudience = true, ValidAudience = config["Jwt:Audience"],
                ValidateLifetime = true, ValidateIssuerSigningKey = true,
                RequireSignedTokens = true, RequireExpirationTime = true,
                IssuerSigningKey = key, ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });
        services.AddAuthorization(); return services;
    }
    public static WebApplication UseServiceScaffold(this WebApplication app, string service)
    {
        app.UseExceptionHandler();
        app.UseAuthentication(); app.UseAuthorization();
        if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
        app.MapHealthChecks("/health/live").AllowAnonymous();
        app.MapGet("/", () => Results.Ok(new { service, phase = "scaffold", businessImplemented = false })).AllowAnonymous();
        return app;
    }
}
internal sealed class ScaffoldExceptionHandler(ILogger<ScaffoldExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken token)
    {
        logger.LogError(exception, "Request failed. TraceId={TraceId}", context.TraceIdentifier);
        var unimplemented = exception is NotImplementedException;
        context.Response.StatusCode = unimplemented ? 501 : 500;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = context.Response.StatusCode,
            Title = unimplemented ? "Feature not implemented in scaffold" : "An unexpected error occurred",
            Extensions = { ["code"] = unimplemented ? "scaffold.not_implemented" : "server.error", ["traceId"] = context.TraceIdentifier }
        }, cancellationToken: token);
        return true;
    }
}
