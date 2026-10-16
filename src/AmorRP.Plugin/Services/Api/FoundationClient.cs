using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;


namespace AmorRP.Plugin.Services.Api;

public sealed class FoundationApiException(HttpStatusCode status, string code, string message, string? requestId = null) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
    public string Code { get; } = code;
    public string? RequestId { get; } = requestId;
    public bool OutcomeUncertain { get; internal set; }
}
public sealed class FoundationClient : IDisposable
{
    private readonly HttpClient http;
    public FoundationClient() : this(new HttpClientHandler { AllowAutoRedirect = false }, TimeSpan.FromSeconds(25)) { }
    internal FoundationClient(HttpMessageHandler handler, TimeSpan timeout)
        => http = new(handler) { Timeout = timeout, MaxResponseContentBufferSize = 1048576 };
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<T> GetAsync<T>(Uri origin, string path, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(origin, path));
        request.Headers.Authorization = new("Bearer", token); return await SendAsync<T>(request, ct);
    }
    public Task<byte[]> GetIconAsync(Uri origin, string path, string token, CancellationToken ct) => GetImageAsync(origin, path, token, 131072, ct);
    public async Task<byte[]> GetImageAsync(Uri origin, string path, string token, int maxBytes, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(origin, path)); request.Headers.Authorization = new("Bearer", token);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); await CheckAsync(response, ct);
        if (response.Content.Headers.ContentType?.MediaType != "image/png") throw new InvalidOperationException("Invalid group image response.");
        if (response.Content.Headers.ContentLength > maxBytes) throw new InvalidOperationException("Group image exceeds its storage limit.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var memory = new MemoryStream(); var buffer = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, maxBytes + 1 - (int)memory.Length)), ct);
            if (read == 0) break;
            memory.Write(buffer, 0, read);
            if (memory.Length > maxBytes) throw new InvalidOperationException("Group image exceeds its storage limit.");
        }
        return memory.ToArray();
    }
    public async Task<string> CommandAsync(Uri origin, CommandRequest command, Guid key, string token, CancellationToken ct)
        => (await CommandWithMetadataAsync(origin, command, key, token, ct)).Json;
    public async Task<CommandResponse> CommandWithMetadataAsync(Uri origin, CommandRequest command, Guid key, string token, CancellationToken ct)
    {
        return await RetryAsync(async () => {
            using var request = new HttpRequestMessage(new HttpMethod(command.Method), new Uri(origin, command.Path));
            request.Headers.Authorization = new("Bearer", token); request.Headers.Add("Idempotency-Key", key.ToString());
            if (command.ETag != null) request.Headers.Add("If-Match", command.ETag);
            if (command.UploadBase64 != null)
            {
                var multipart = new MultipartFormDataContent(); var file = new ByteArrayContent(Convert.FromBase64String(command.UploadBase64));
                file.Headers.ContentType = new("application/octet-stream"); multipart.Add(file, "file", "currency-icon"); request.Content = multipart;
            }
            else if (command.Json != null) request.Content = new StringContent(command.Json, System.Text.Encoding.UTF8, "application/json");
            using var response = await http.SendAsync(request, ct); await CheckAsync(response, ct); return new CommandResponse(await response.Content.ReadAsStringAsync(ct), response.Headers.TryGetValues("Idempotency-Replayed", out var replay) && replay.Contains("true"));
        }, ct);
    }
    public async Task<LoginAttemptCreated> StartAsync(Uri origin, LoginStart selected, CancellationToken ct)
    {
        var key = Guid.CreateVersion7(); var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');
        var response = await RetryAsync(async () => {
            using var r = new HttpRequestMessage(HttpMethod.Post, new Uri(origin, "api/v1/auth/login-attempts"));
            r.Headers.Add("Idempotency-Key", key.ToString()); r.Headers.Add("X-Login-Request-Credential", secret); r.Content = JsonContent.Create(selected);
            return await SendAsync<CommandResult<LoginAttemptCreated>>(r, ct);
        }, ct);
        if (!XivAuthAuthorization.IsTrusted(response.Result.AuthorizationUrl)) throw new InvalidOperationException("Server supplied an untrusted login page.");
        if (!response.Result.PairingRequired) throw new InvalidOperationException("The Amor RP server needs the login-pairing security update before a new login can start.");
        return response.Result;
    }
    public async Task WaitForVerificationAsync(Uri origin, LoginAttemptCreated attempt, CancellationToken ct)
    {
        while (DateTimeOffset.UtcNow < attempt.ExpiresAt)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(attempt.PollIntervalSeconds, 3, 10)), ct);
            using var r = new HttpRequestMessage(HttpMethod.Get, new Uri(origin, $"api/v1/auth/login-attempts/{attempt.AttemptId}"));
            r.Headers.Add("X-Login-Attempt-Credential", attempt.AttemptCredential);
            var state = await SendAsync<LoginAttemptStatus>(r, ct);
            if (state.Status == "pending") continue;
            if (state.Status != "verified") throw new InvalidOperationException(state.FailureCode == "identity_binding_changed" ? "XIVAuth ownership changed. Existing assets remain protected; contact the operator for recovery." : "Selected character could not be verified. Check XIVAuth and start login again.");
            return; // Approval alone must never redeem a session.
        }
        throw new InvalidOperationException("Login attempt expired. Start again.");
    }
    public async Task<SessionTokens> ExchangeLoginAsync(Uri origin, LoginAttemptCreated attempt, string pairingCode, Guid key, CancellationToken ct)
    {
        if (!attempt.PairingRequired || string.IsNullOrWhiteSpace(pairingCode)) throw new InvalidOperationException("Enter the pairing code from your browser.");
        return (await RetryAsync(async () => {
            using var exchange = new HttpRequestMessage(HttpMethod.Post, new Uri(origin, $"api/v1/auth/login-attempts/{attempt.AttemptId}/exchange"));
            exchange.Headers.Add("Idempotency-Key", key.ToString()); exchange.Headers.Add("X-Login-Attempt-Credential", attempt.AttemptCredential);
            exchange.Content = JsonContent.Create(new LoginExchangeRequest(pairingCode));
            return await SendAsync<CommandResult<SessionTokens>>(exchange, ct);
        }, ct)).Result;
    }
    public async Task<SessionTokens> RenewAsync(Uri origin, SessionTokens tokens, Guid key, CancellationToken ct)
    {
        var command = new CommandRequest("POST", "api/v1/auth/sessions/refresh", JsonSerializer.Serialize(new RefreshRequest(tokens.RefreshToken), Json), null);
        var response = await CommandAsync(origin, command, key, "", ct);
        return JsonSerializer.Deserialize<CommandResult<SessionTokens>>(response, Json)!.Result;
    }
    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await http.SendAsync(request, ct); await CheckAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(Json, ct) ?? throw new InvalidOperationException("Invalid server response.");
    }
    private static async Task CheckAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        string? requestId = null; var code = "request_failed"; var title = "Request failed. Refresh and try again.";
        try {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (json.RootElement.TryGetProperty("requestId", out var id)) requestId = id.GetString();
            if (json.RootElement.TryGetProperty("code", out var c)) code = c.GetString() ?? code;
            if (json.RootElement.TryGetProperty("title", out var t)) title = t.GetString() ?? title;
        } catch (JsonException) { }
        throw new FoundationApiException(response.StatusCode, code, title.Length <= 300 ? title : "Request failed.", requestId);
    }
    private static async Task<T> RetryAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { return await operation(); }
            catch (Exception ex) when (attempt < 2 && !ct.IsCancellationRequested &&
                (ex is HttpRequestException or TaskCanceledException || ex is FoundationApiException { Status: HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout }))
            { await Task.Delay(TimeSpan.FromSeconds(attempt + 1), ct); }
            catch (FoundationApiException failure) when (attempt > 0)
            {
                // A lost response or gateway failure may have followed a commit.
                // A later rejection cannot prove that the original attempt had no effect.
                failure.OutcomeUncertain = true; throw;
            }
        }
    }
    public void Dispose() => http.Dispose();
}

public sealed record CommandResponse(string Json, bool Replayed);
