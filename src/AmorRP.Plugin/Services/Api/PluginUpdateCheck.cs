using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AmorRP.Plugin.Services.Api;

internal enum PluginUpdateStatus { Checking, Current, Required, Unavailable }

internal sealed class PluginUpdateCheck(Version installedVersion, HttpMessageHandler? handler = null) : IDisposable
{
    private const string FeedUrl = "https://raw.githubusercontent.com/Runmih/AmorRP/main/repo.json";
    private readonly HttpClient http = new(handler ?? new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    })
    {
        Timeout = TimeSpan.FromSeconds(8),
        MaxResponseContentBufferSize = 64 * 1024
    };
    private readonly BackgroundRefresh<Version> refresh = new(TimeSpan.FromMinutes(5));

    public Version InstalledVersion { get; } = installedVersion;
    public Version? PublishedVersion { get; private set; }
    public bool IsChecking => refresh.IsLoading;
    public PluginUpdateStatus Status => PublishedVersion > InstalledVersion ? PluginUpdateStatus.Required
        : refresh.Error != null ? PluginUpdateStatus.Unavailable
        : PublishedVersion != null ? PluginUpdateStatus.Current : PluginUpdateStatus.Checking;

    public void Tick(DateTimeOffset now)
    {
        if (refresh.TryCollect(now, 0, out var version) && (PublishedVersion == null || version > PublishedVersion))
            PublishedVersion = version;
        if (Status != PluginUpdateStatus.Required)
            refresh.Start(now, 0, CancellationToken.None, ReadVersion);
    }

    public void Retry()
    {
        if (!IsChecking) refresh.Invalidate();
    }

    private async Task<Version> ReadVersion(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, FeedUrl);
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        try
        {
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var entries = await response.Content.ReadFromJsonAsync<FeedEntry[]>(cancellationToken).ConfigureAwait(false);
            if (entries is not [{ InternalName: "AmorRP" } entry]
                || !Version.TryParse(entry.AssemblyVersion, out var version) || version.Revision < 0)
                throw new InvalidDataException("The Amor RP installer feed did not report a valid release version.");
            return version;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException("The Amor RP installer feed did not respond in time.", exception);
        }
    }

    private sealed record FeedEntry(string? InternalName, string? AssemblyVersion);

    public void Dispose()
    {
        refresh.Dispose();
        http.Dispose();
    }
}
