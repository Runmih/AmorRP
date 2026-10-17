using System.Text.Json;
using AmorRP.Contracts.Common;
using AmorRP.Plugin.Services.Api;
using Xunit;
namespace AmorRP.Plugin.Tests;

public sealed class CommunityCompatibilityTests
{
    [Fact]
    public void Legacy_receipts_and_pending_commands_are_converted_without_changing_user_text_or_the_wire_request()
    {
        const string old = """{"groupId":"id","myCapabilities":["group.moderate"],"description":"group.moderate","name":"My group","etag":"\"group:id:2\""}""";
        var current = LegacyCommunityJson.ToCurrent(old); using var json = JsonDocument.Parse(current);
        Assert.Equal("id", json.RootElement.GetProperty("communityId").GetString());
        Assert.Equal("community.moderate", json.RootElement.GetProperty("myCapabilities")[0].GetString());
        Assert.Equal("group.moderate", json.RootElement.GetProperty("description").GetString());
        Assert.Equal("My group", json.RootElement.GetProperty("name").GetString());
        Assert.Equal(JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(old)), LegacyCommunityJson.ToLegacy(current));
        var request = new CommandRequest("PUT", "api/v1/groups/id/members/id/capabilities", """{"capabilities":["group.moderate"]}""", "\"group:id:2\"");
        var logical = request.CurrentShape(); Assert.StartsWith("api/v2/communities/", logical.Path); Assert.Contains("community.moderate", logical.Json!);
        Assert.Equal("api/v1/groups/id/members/id/capabilities", request.Path); Assert.Contains("group.moderate", request.Json!);
    }
    [Fact]
    public void New_group_commands_do_not_get_mistaken_for_old_communities()
    {
        var request = new CommandRequest("PATCH", "api/v2/communities/c/groups/g", "{}", "\"group:g:2\"");
        Assert.Same(request, request.CurrentShape()); Assert.Equal("\"group:g:2\"", request.CurrentShape().ETag);
    }
}
