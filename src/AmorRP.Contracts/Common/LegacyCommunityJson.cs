using System.Text;
using System.Text.Json;

namespace AmorRP.Contracts.Common;

// Explicit compatibility boundary for stable clients and encrypted receipts written before API v2.
// Domain models use Community consistently; user-authored text is never renamed.
public static class LegacyCommunityJson
{
    public static string ToCurrent(string json) => Convert(json, legacy: false);
    public static string ToLegacy(string json) => Convert(json, legacy: true);
    public static string CurrentPath(string path) => path.StartsWith("api/v1/", StringComparison.Ordinal)
        ? path.Replace("api/v1/", "api/v2/", StringComparison.Ordinal).Replace("/groups", "/communities", StringComparison.Ordinal) : path;
    public static string ETag(string text, bool legacy) => legacy
        ? text.Replace("community", "group", StringComparison.Ordinal) : text.Replace("group", "community", StringComparison.Ordinal);
    private static string Convert(string json, bool legacy)
    {
        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, document.RootElement, legacy, null);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static void Write(Utf8JsonWriter writer, JsonElement value, bool legacy, string? property)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var p in value.EnumerateObject())
                {
                    var key = legacy ? p.Name.Replace("Communities", "Groups", StringComparison.Ordinal).Replace("Community", "Group", StringComparison.Ordinal).Replace("communities", "groups", StringComparison.Ordinal).Replace("community", "group", StringComparison.Ordinal)
                        : p.Name.Replace("Groups", "Communities", StringComparison.Ordinal).Replace("Group", "Community", StringComparison.Ordinal).Replace("groups", "communities", StringComparison.Ordinal).Replace("group", "community", StringComparison.Ordinal);
                    writer.WritePropertyName(key); Write(writer, p.Value, legacy, p.Name);
                }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Write(writer, item, legacy, property); writer.WriteEndArray(); break;
            case JsonValueKind.String:
                var text = value.GetString()!;
                if (property == "apiVersion") text = legacy ? "1" : "2";
                else if (property is "capabilities" or "myCapabilities" or "grantableCapabilities" && text == (legacy ? "community.moderate" : "group.moderate")) text = legacy ? "group.moderate" : "community.moderate";
                else if (property is "etag" or "eTag") text = ETag(text, legacy);
                else if (property is "kind" or "code") text = legacy ? text.Replace("community", "group", StringComparison.Ordinal) : text.Replace("group", "community", StringComparison.Ordinal);
                writer.WriteStringValue(text); break;
            default: value.WriteTo(writer); break;
        }
    }
}
