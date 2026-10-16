namespace AmorRP.Plugin.Services.Api;

public static class BackendAddress
{
    public static bool TryParse(string input, out Uri? address)
    {
        address = null;
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var candidate)
            || (candidate.Scheme != Uri.UriSchemeHttps
                && !(candidate.Scheme == Uri.UriSchemeHttp && candidate.IsLoopback))
            || candidate.UserInfo.Length != 0 || candidate.Query.Length != 0
            || candidate.Fragment.Length != 0 || candidate.AbsolutePath != "/")
            return false;
        address = candidate;
        return true;
    }
}
