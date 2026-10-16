namespace AmorRP.Server.Features.Feasibility;

public sealed class XivAuthOptions
{
    public bool DurableEnabled { get; set; }
    public string DurableCallbackUrl { get; set; } = "";
    public bool DurableIsValid() => !DurableEnabled || (!string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret) && Uri.TryCreate(DurableCallbackUrl, UriKind.Absolute, out var url)
        && url.Scheme == "https" && url.UserInfo.Length == 0 && url.Query.Length == 0
        && url.Fragment.Length == 0 && url.AbsolutePath == "/auth/xivauth/callback");

    public bool Enabled { get; set; }
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string CallbackUrl { get; set; } = "";

    public bool IsValid() => !Enabled || (!string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && Uri.TryCreate(CallbackUrl, UriKind.Absolute, out var callback)
        && callback.Scheme == "https" && callback.UserInfo.Length == 0
        && callback.AbsolutePath == "/auth/xivauth/feasibility-callback"
        && callback.Query.Length == 0 && callback.Fragment.Length == 0);
}
