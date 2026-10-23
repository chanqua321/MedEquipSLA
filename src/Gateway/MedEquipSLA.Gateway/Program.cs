var builder = WebApplication.CreateBuilder(args);
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
var app = builder.Build();
app.MapGet("/health/live", () => Results.Ok(new { status = "live", phase = "scaffold" }));
app.MapReverseProxy();
app.Run();
