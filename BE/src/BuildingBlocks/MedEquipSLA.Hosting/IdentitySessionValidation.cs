using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;

namespace MedEquipSLA.Hosting;

public static class IdentitySessionValidation
{
    public static void AddIdentitySessionValidation(this IServiceCollection services, IConfiguration configuration)
    {
        const string clientName = "IdentitySession";
        services.AddHttpClient(clientName, client =>
        {
            client.BaseAddress = new Uri(configuration["Identity:Address"] ?? "http://localhost:5101");
            client.Timeout = TimeSpan.FromSeconds(2);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events.OnTokenValidated = async context =>
            {
                var http = context.HttpContext;
                var factory = http.RequestServices.GetRequiredService<IHttpClientFactory>();
                using var client = factory.CreateClient(clientName);
                using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
                // Forward the token which was actually validated. SignalR may use a query token;
                // forwarding a different Authorization header would validate the wrong session.
                var rawToken = context.SecurityToken switch
                {
                    JsonWebToken json => json.EncodedToken,
                    JwtSecurityToken jwt => jwt.RawData,
                    _ => null
                };
                if (string.IsNullOrWhiteSpace(rawToken)) { context.Fail("Validated session token is required."); return; }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rawToken);
                try
                {
                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, http.RequestAborted);
                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                        context.Fail("Current authorization is required.");
                }
                catch (HttpRequestException)
                {
                    context.Fail("Current authorization could not be verified.");
                }
                catch (OperationCanceledException) when (!http.RequestAborted.IsCancellationRequested)
                {
                    context.Fail("Current authorization could not be verified.");
                }
            };
        });
    }
}
