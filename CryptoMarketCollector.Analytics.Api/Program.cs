using CryptoMarketCollector.Analytics.Api.Options;
using CryptoMarketCollector.Analytics.Api.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AnalyticsOptions>(
    builder.Configuration.GetSection("Analytics"));

builder.Services.AddSingleton<MarketAnalyticsService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalReact", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

WebApplication app = builder.Build();

app.UseCors("LocalReact");

app.MapGet("/", () => Results.Redirect("/api/analytics/overview"));

app.MapGet(
    "/api/analytics/overview",
    async (
        MarketAnalyticsService analyticsService,
        CancellationToken cancellationToken) =>
    {
        return Results.Ok(
            await analyticsService.GetOverviewAsync(cancellationToken));
    });

app.Run();