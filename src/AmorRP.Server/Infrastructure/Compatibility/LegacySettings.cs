using AmorRP.Server.Options;
namespace AmorRP.Server.Infrastructure.Compatibility;

public static class LegacySettings
{
    // Existing Render policy overrides keep their configured priority after the
    // option names change. Explicit new names at the same priority win.
    public static void Apply(ConfigurationManager configuration)
    {
        var providers = ((IConfigurationRoot)configuration).Providers.Reverse().ToArray();
        var aliases = new Dictionary<string, string?>();
        foreach (var property in typeof(ServicePolicyOptions).GetProperties())
        {
            if (!property.Name.Contains("Community", StringComparison.Ordinal) && !property.Name.Contains("Communities", StringComparison.Ordinal)) continue;
            var current = "ServicePolicy:" + property.Name;
            var old = "ServicePolicy:" + property.Name.Replace("Communities", "Groups", StringComparison.Ordinal).Replace("Community", "Group", StringComparison.Ordinal);
            foreach (var provider in providers)
            {
                if (provider.TryGet(current, out _)) break;
                if (provider.TryGet(old, out var value)) { aliases[current] = value; break; }
            }
        }
        if (aliases.Count > 0) configuration.AddInMemoryCollection(aliases);
    }
}
