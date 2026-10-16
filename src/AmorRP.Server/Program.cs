using AmorRP.Server.Features.Announcements;
using AmorRP.Server.Features.Trading;
using AmorRP.Server.Features.Catalog;
using AmorRP.Server.Features.Inventory;
using AmorRP.Server.Features.Media;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Groups;
using AmorRP.Server.Features.Currency;
using AmorRP.Server.Features.History;
using AmorRP.Server.Infrastructure.Security;
using System.Text.Json.Serialization;
using AmorRP.Server.Features.Capabilities;
using AmorRP.Server.Features.Health;
using AmorRP.Server.Features.Feasibility;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;

var migrate = args.Contains("--migrate", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate").ToArray());
if (builder.Configuration["PORT"] is { } port && string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_URLS"]))
{
    if (!int.TryParse(port, out var number) || number is < 1 or > 65535)
        throw new InvalidOperationException("PORT must be a valid TCP port.");
    builder.WebHost.UseUrls($"http://0.0.0.0:{number}");
}
var connectionString = builder.Configuration.GetConnectionString("Database");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Configure ConnectionStrings__Database before starting the server.");

builder.Services.AddOptions<SessionPolicyOptions>().BindConfiguration("SessionPolicy").ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<ServicePolicyOptions>()
    .BindConfiguration("ServicePolicy").ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddDbContext<AmorDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<DatabaseReadiness>();
builder.Services.AddOptions<HealthPolicyOptions>().BindConfiguration("HealthPolicy").ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<ReadinessMonitor>();
builder.Services.AddHostedService(services => services.GetRequiredService<ReadinessMonitor>());
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
builder.Services.AddScoped<SecretVault>();
builder.Services.AddOptions<SecretVaultOptions>().BindConfiguration("SecretVault")
    .Validate(o => !builder.Configuration.GetValue<bool>("XivAuth:DurableEnabled") || o.IsValid(), "Durable authentication requires a configured 32-byte encryption key ring.").ValidateOnStart();
builder.Services.AddScoped<CommandStore>();
builder.Services.AddScoped<PageCursor>();
builder.Services.AddScoped<SessionAccess>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<LoginAdmission>();
builder.Services.AddHostedService<LoginOriginCleanupWorker>();
builder.Services.AddScoped<GroupAccess>();
builder.Services.AddScoped<GroupService>();
builder.Services.AddScoped<AnnouncementService>();
builder.Services.AddScoped<CurrencyService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<CurrencyIconService>();
builder.Services.AddScoped<GroupLogoService>();
builder.Services.AddScoped<HistoryService>();
builder.Services.AddScoped<TradeService>();
builder.Services.AddScoped<TradeReservations>();
builder.Services.AddHostedService<TradeExpiryWorker>();
builder.Services.AddHttpClient<IDurableIdentityProvider, DurableXivAuthProvider>(client => {
    client.Timeout = TimeSpan.FromSeconds(15); client.MaxResponseContentBufferSize = 65536;
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    if (!context.ProblemDetails.Extensions.ContainsKey("code")) context.ProblemDetails.Extensions["code"] = context.ProblemDetails.Status switch
    {
        401 => "unauthorized", 409 => "conflict", 429 => "rate_limited",
        503 => "service_unavailable", >= 500 => "server_error", _ => "invalid_request"
    };
    context.ProblemDetails.Extensions["requestId"] = context.HttpContext.TraceIdentifier;
});
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize =
    (builder.Configuration.GetSection("ServicePolicy").Get<ServicePolicyOptions>() ?? new()).MaxRequestBytes);
// OAuth codes/state and provider grants must not appear in request/client logs.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Services.AddOptions<XivAuthOptions>().BindConfiguration("XivAuth")
    .Validate(options => options.IsValid(), "Enabled XIVAuth requires client credentials and the exact HTTPS feasibility callback.")
    .Validate(options => options.DurableIsValid(), "Durable XIVAuth requires client credentials and the exact HTTPS callback.")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IdentityProbeStore>();
builder.Services.AddHttpClient<ICharacterIdentityProvider, XivAuthProvider>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.MaxResponseContentBufferSize = 65536;
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddClientRateLimiting(builder.Configuration);

var app = builder.Build();
if (migrate)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Database migrations applied.");
    return;
}

app.UseExceptionHandler();
app.Use(async (context, next) => {
    try { await next(context); }
    catch (ApiFault fault) when (!context.Response.HasStarted) {
        context.Response.Headers.CacheControl = "no-store";
        if (fault.Status == 429) context.Response.Headers.RetryAfter = "60";
        await Results.Problem(statusCode: fault.Status, title: fault.Message,
            extensions: new Dictionary<string, object?> { ["code"] = fault.Code, ["requestId"] = context.TraceIdentifier }).ExecuteAsync(context);
    }
});
app.UseStatusCodePages();
app.UseForwardedHeaders();
app.UseMiddleware<RenderClientIpMiddleware>();
app.UseRateLimiter();
app.MapHealthEndpoints();
app.MapCapabilitiesEndpoints();
app.MapFeasibilityEndpoints();
app.MapAuthenticationEndpoints();
app.MapGroupEndpoints();
app.MapAnnouncementEndpoints();
app.MapCurrencyEndpoints();
app.MapCatalogEndpoints();
app.MapInventoryEndpoints();
app.MapCurrencyIconEndpoints();
app.MapGroupLogoEndpoints();
app.MapHistoryEndpoints();
app.MapTradeEndpoints();
await app.RunAsync();

public partial class Program;
