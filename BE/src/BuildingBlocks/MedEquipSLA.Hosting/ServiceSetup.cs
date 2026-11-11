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
using Microsoft.OpenApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Swashbuckle.AspNetCore.SwaggerGen;
namespace MedEquipSLA.Hosting;
public static class ServiceSetup
{
    public static IServiceCollection AddHubQueryTokenSupport(this IServiceCollection services, string path)
    {
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events.OnMessageReceived = context =>
            {
                if (context.HttpContext.Request.Path.StartsWithSegments(path) &&
                    context.Request.Query["access_token"] is { Count: 1 } token && !string.IsNullOrEmpty(token[0]))
                    context.Token = token[0];
                return Task.CompletedTask;
            };
        });
        return services;
    }
    public static IServiceCollection AddServiceScaffold(this IServiceCollection services, IConfiguration config)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<ScaffoldExceptionHandler>();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.CustomSchemaIds(SchemaName);
            options.OperationFilter<EndpointAuthorizationDocumentation>();
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Paste the access token from Identity."
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        });
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
    private static string SchemaName(Type type)
    {
        if (type.IsArray) return "Array." + SchemaName(type.GetElementType()!);
        var name = (type.FullName ?? type.Name).Split('`')[0].Replace('+', '.');
        return type.IsGenericType ? name + "." + string.Join(".", type.GetGenericArguments().Select(SchemaName)) : name;
    }
    public static WebApplication UseServiceScaffold(this WebApplication app, string service)
    {
        app.UseExceptionHandler();
        app.UseAuthentication(); app.UseAuthorization();
        if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
        app.MapHealthChecks("/health/live").AllowAnonymous();
        app.MapGet("/", () => Results.Ok(new { service, apiVersion = "v1", health = "/health/live" })).AllowAnonymous();
        return app;
    }
}
internal sealed class EndpointAuthorizationDocumentation : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any()) operation.Security = [];
        var roleRequirements = metadata.OfType<AuthorizationPolicy>().SelectMany(x => x.Requirements)
            .OfType<RolesAuthorizationRequirement>().Select(x => string.Join(" or ", x.AllowedRoles)).Distinct().ToArray();
        var roles = metadata.OfType<IAuthorizeData>().Where(x => !string.IsNullOrWhiteSpace(x.Roles)).Select(x => x.Roles!).ToArray();
        if (roleRequirements.Length + roles.Length > 0)
            operation.Description = (operation.Description ?? "") + " Authorization requirements (all apply): " +
                string.Join("; ", roleRequirements.Concat(roles)) + ".";
        if (context.ApiDescription.RelativePath?.StartsWith("api/internal/", StringComparison.Ordinal) == true)
        {
            operation.Description = (operation.Description ?? "") + " Service-only read endpoint; internal lookup credential required.";
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter { Name = "X-Recipient-Lookup-Key", In = ParameterLocation.Header,
                Required = true, Schema = new OpenApiSchema { Type = JsonSchemaType.String }, Description = "Internal service credential, never a user JWT or client-side secret." });
        }
    }
}
internal sealed class ScaffoldExceptionHandler(ILogger<ScaffoldExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken token)
    {
        if (exception is BadHttpRequestException invalid)
        {
            await Results.Problem(statusCode: invalid.StatusCode, title: "Invalid request").ExecuteAsync(context);
            return true;
        }
        logger.LogError(exception, "Request failed. TraceId={TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = context.Response.StatusCode,
            Title = "An unexpected error occurred",
            Extensions = { ["code"] = "server.error", ["traceId"] = context.TraceIdentifier }
        }, cancellationToken: token);
        return true;
    }
}
