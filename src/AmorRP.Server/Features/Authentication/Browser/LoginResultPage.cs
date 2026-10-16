using System.Security.Cryptography;
using System.Text;
using System.Net;
using Microsoft.Net.Http.Headers;

namespace AmorRP.Server.Features.Authentication.Browser;

/// <summary>Self-contained browser result; only the verifying browser receives the pairing secret.</summary>
internal static class LoginResultPage
{
    private static readonly string Template = ReadTemplate();
    private static readonly byte[] BackgroundBytes = ReadBackground();
    private static readonly string BackgroundHash = Convert.ToHexString(SHA256.HashData(BackgroundBytes)).ToLowerInvariant();
    public static string BackgroundPath => $"/auth/assets/login-background-{BackgroundHash[..12]}.jpg";

    public static IResult Background(HttpContext http, string contentHash)
    {
        if (!string.Equals(contentHash, BackgroundHash[..12], StringComparison.Ordinal)) return Results.NotFound();
        http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        http.Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Results.Bytes(BackgroundBytes, "image/jpeg", entityTag: new EntityTagHeaderValue($"\"{BackgroundHash}\""));
    }

    public static IResult Render(HttpContext http, bool verified, int statusCode = StatusCodes.Status200OK, string? pairingCode = null)
    {
        verified = verified && pairingCode != null;
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers["Referrer-Policy"] = "no-referrer";
        http.Response.Headers["X-Content-Type-Options"] = "nosniff";
        http.Response.Headers["Content-Security-Policy"] =
            $"default-src 'none'; script-src 'nonce-{nonce}'; style-src 'nonce-{nonce}'; img-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

        var html = Template.Replace("{{nonce}}", nonce, StringComparison.Ordinal)
            .Replace("{{backgroundPath}}", BackgroundPath, StringComparison.Ordinal)
            .Replace("{{verified}}", verified ? "true" : "false", StringComparison.Ordinal)
            .Replace("{{title}}", verified ? "Login verified" : "Login not completed", StringComparison.Ordinal)
            .Replace("{{status}}", verified ? "Character verified" : "Verification not completed", StringComparison.Ordinal)
            .Replace("{{heading}}", verified ? "Pair your plugin." : "Let’s try that again.", StringComparison.Ordinal)
            .Replace("{{pairingCode}}", WebUtility.HtmlEncode(pairingCode ?? ""), StringComparison.Ordinal)
            .Replace("{{closing}}", verified ? "Keep this page open until you finish pairing in Amor RP." : "You can close this tab and return to Amor RP to try again.", StringComparison.Ordinal)
            .Replace("{{description}}", verified
                ? "Copy this one-time code into <strong>Amor RP</strong> on the computer where you started login. Approving XIVAuth alone does not sign the plugin in."
                : "This login was cancelled, expired or could not be verified. Return to <strong>Amor RP</strong> to check its status and try signing in again.", StringComparison.Ordinal);
        return Results.Content(html, "text/html", Encoding.UTF8, statusCode);
    }

    private static string ReadTemplate()
    {
        using var resource = typeof(LoginResultPage).Assembly.GetManifestResourceStream("AmorRP.LoginResultPage.html")
            ?? throw new InvalidOperationException("The login result page is missing from the server package.");
        using var reader = new StreamReader(resource, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static byte[] ReadBackground()
    {
        using var resource = typeof(LoginResultPage).Assembly.GetManifestResourceStream("AmorRP.LoginBackground.jpg")
            ?? throw new InvalidOperationException("The login background is missing from the server package.");
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        return buffer.ToArray();
    }
}
