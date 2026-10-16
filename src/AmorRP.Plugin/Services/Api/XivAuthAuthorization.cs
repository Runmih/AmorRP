namespace AmorRP.Plugin.Services.Api;

public static class XivAuthAuthorization
{
    public static bool IsTrusted(string value) => Uri.TryCreate(value, UriKind.Absolute, out var url)
        && url.Scheme == "https" && url.Host == "xivauth.net" && url.IsDefaultPort
        && url.AbsolutePath == "/oauth/authorize" && url.UserInfo.Length == 0 && url.Fragment.Length == 0;
}
