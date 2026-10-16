namespace AmorRP.Plugin.Services.Api;

// Release endpoint, maintained in one place. Existing credentials and pending
// commands remain bound to their saved origin; never rewrite those bindings.
public static class ProductServer
{
    public const string Origin = "https://amorrp-server.onrender.com/";
}
