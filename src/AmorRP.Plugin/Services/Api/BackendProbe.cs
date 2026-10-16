using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;

namespace AmorRP.Plugin.Services.Api;

public sealed record ProbeResult(string Summary, bool IsReady);

public sealed class BackendProbe : IDisposable
{
    private readonly HttpClient client;

    public BackendProbe() : this(new HttpClientHandler { AllowAutoRedirect = false }, TimeSpan.FromSeconds(5)) { }

    internal BackendProbe(HttpMessageHandler handler, TimeSpan timeout)
    {
        client = new HttpClient(handler) { Timeout = timeout, MaxResponseContentBufferSize = 65536 };
    }

    public async Task<ProbeResult> CheckAsync(Uri origin, CancellationToken cancellationToken)
    {
        try
        {
            using var live = await client.GetAsync(new Uri(origin, "health/live"), cancellationToken);
            if (!live.IsSuccessStatusCode)
                return new("Server did not pass its liveness check.", false);
            var health = await live.Content.ReadFromJsonAsync<HealthResponse>(cancellationToken);
            if (health?.Status != "ok")
                return new("Server returned an unexpected health response.", false);
            using var discovery = await client.GetAsync(new Uri(origin, "api/v1/capabilities"), cancellationToken);
            if (!discovery.IsSuccessStatusCode)
                return new("Server discovery failed. Check the address and M0 setup guide.", false);
            var capabilities = await discovery.Content.ReadFromJsonAsync<CapabilitiesResponse>(cancellationToken);
            if (capabilities?.ApiVersion != "1")
                return new("Server protocol is incompatible with this plugin.", false);
            if (!Version.TryParse(capabilities.MinimumClientVersion, out var minimum)
                || minimum > typeof(BackendProbe).Assembly.GetName().Version)
                return new("This server requires a newer plugin. Update Amor RP before connecting.", false);
            if (!Version.TryParse(capabilities.ServerVersion, out var serverVersion) || serverVersion < new Version(0, 0, 6, 0))
                return new("M5 requires server 0.0.6.0 or newer. Upgrade the server before testing this plugin.", false);
            if (capabilities.Maintenance) return new(capabilities.Message, false);
            using var ready = await client.GetAsync(new Uri(origin, "health/ready"), cancellationToken);
            if (ready.StatusCode == HttpStatusCode.ServiceUnavailable)
                return new("Server is running; database is not ready. Follow the M0 database setup guide.", false);
            if (!ready.IsSuccessStatusCode
                || (await ready.Content.ReadFromJsonAsync<HealthResponse>(cancellationToken))?.Status != "ok")
                return new("Server did not pass its readiness check.", false);
            return new($"Server {capabilities.ServerVersion} and database are ready. Connection test passed.", true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new("Connection timed out. Check that the local server is running.", false);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or NotSupportedException)
        {
            return new("Could not reach a compatible server. Check the address and M0 setup guide.", false);
        }
    }

    public void Dispose() => client.Dispose();
}
