using System.Text.Json;
using System.Text.RegularExpressions;
using AmorRP.Plugin.Services.Api;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class CommandRoutesTests
{
    private const string Id = "11111111-1111-4111-8111-111111111111";
    private static CommandOperation Resolve(string method, string suffix) =>
        new CommandRequest(method, $"api/v2/communities/{Id}/" + suffix.Replace("{id}", Id), null, null).Operation;

    [Fact]
    public void Every_registered_route_has_one_match_and_every_operation_is_registered()
    {
        foreach (var route in CommandRoutes.All)
        {
            var path = Regex.Replace(route.Template, "\\{[^}]+\\}", Id);
            var matches = CommandRoutes.All.Where(x => x.Matches(route.Method, path.Split('/'))).ToArray();
            Assert.Single(matches);
            Assert.Equal(route.Operation, new CommandRequest(route.Method, path, null, null).Operation);
        }
        Assert.Equal(Enum.GetValues<CommandKind>().Where(x => x != CommandKind.Unknown).Order(),
            CommandRoutes.All.Select(x => x.Operation.Kind).Distinct().Order());
    }

    [Fact]
    public void Group_activity_receipts_never_use_the_group_profile_or_member_deserializer()
    {
        Assert.Equal(CommandHandler.GroupActivity, Resolve("POST", "groups/{id}/join").Handler);
        Assert.Equal(CommandHandler.GroupActivity, Resolve("POST", "groups/{id}/announcements").Handler);
        Assert.Equal(CommandHandler.GroupProfile, Resolve("DELETE", "groups/{id}/logo").Handler);
        Assert.Equal(CommandHandler.GroupProfile, Resolve("DELETE", "groups/{id}").Handler);
        Assert.Equal(CommandHandler.Home, Resolve("POST", "announcements").Handler);
        Assert.Equal(CommandHandler.Member, Resolve("DELETE", "members/{id}").Handler);
        Assert.Equal(CommandHandler.Inventory, Resolve("PATCH", "members/{id}/holdings/{id}").Handler);
    }

    [Theory]
    [InlineData("POST", "trades/{id}/final-confirm", (int)CommandKind.FinalConfirmTrade)]
    [InlineData("POST", "trades/{id}/confirm", (int)CommandKind.ConfirmTrade)]
    [InlineData("PUT", "members/{id}/block", (int)CommandKind.BlockMember)]
    [InlineData("DELETE", "members/{id}/block", (int)CommandKind.UnblockMember)]
    [InlineData("PUT", "groups/{id}/favorite", (int)CommandKind.FavoriteGroup)]
    [InlineData("DELETE", "groups/{id}/favorite", (int)CommandKind.UnfavoriteGroup)]
    public void Verb_and_full_route_distinguish_similar_actions(string method, string path, int expected) =>
        Assert.Equal((CommandKind)expected, Resolve(method, path).Kind);

    [Theory]
    [InlineData("POST", "something/trades/{id}/confirm")]
    [InlineData("PUT", "groups/{id}/notice/extra")]
    [InlineData("PATCH", "groups/not-an-id")]
    [InlineData("GET", "groups/{id}/events")]
    public void Unrecognized_routes_cannot_select_a_response_handler(string method, string path)
    {
        var operation = Resolve(method, path);
        Assert.Equal(CommandKind.Unknown, operation.Kind);
        Assert.Equal(CommandHandler.None, operation.Handler);
    }

    [Fact]
    public void Treasury_movements_select_their_typed_handler_and_keep_group_scope()
    {
        foreach (var suffix in new[] { "deposits", "withdrawals", "return" })
        {
            var request = new CommandRequest("POST", $"api/v2/communities/{Id}/groups/{Id}/treasury/" + suffix, null, null);
            Assert.Equal(CommandHandler.Treasury, request.Operation.Handler);
            Assert.Equal(RefreshTargets.Community, request.Operation.Refresh);
            Assert.True(request.AppliesToGroup(Guid.Parse(Id)));
            Assert.False(request.AppliesToGroup(Guid.NewGuid()));
        }
        Assert.Equal(CommandKind.DepositTreasury, Resolve("POST", "groups/{id}/treasury/deposits").Kind);
        Assert.Equal(CommandKind.WithdrawTreasury, Resolve("POST", "groups/{id}/treasury/withdrawals").Kind);
        Assert.Equal(CommandKind.CollectTreasuryReturn, Resolve("POST", "groups/{id}/treasury/return").Kind);
    }

    [Fact]
    public void Refresh_targets_are_scoped_to_the_affected_features()
    {
        Assert.Equal(RefreshTargets.Groups, Resolve("PUT", "groups/{id}/notice").Refresh);
        Assert.Equal(RefreshTargets.Groups | RefreshTargets.Community, Resolve("DELETE", "groups/{id}").Refresh);
        Assert.Equal(RefreshTargets.Community | RefreshTargets.Inventory, Resolve("PUT", "trades/{id}/offers/mine").Refresh);
        Assert.Equal(RefreshTargets.Community | RefreshTargets.Members, Resolve("PUT", "members/{id}/capabilities").Refresh);
        Assert.Equal(RefreshTargets.Community | RefreshTargets.CommunityLogo, Resolve("PUT", "logo").Refresh);
        Assert.Equal(RefreshTargets.Community | RefreshTargets.CurrencyIcon, Resolve("PUT", "currency/icon").Refresh);
    }

    [Fact]
    public void Restored_requests_keep_their_wire_identity_and_need_no_new_saved_fields()
    {
        var legacy = new CommandRequest("POST", $"api/v1/groups/{Id}/trades", "{\"targetCharacterId\":\"" + Id + "\"}", "\"old\"");
        var serialized = JsonSerializer.Serialize(legacy, FoundationClient.Json);
        var restored = JsonSerializer.Deserialize<CommandRequest>(serialized, FoundationClient.Json)!;
        Assert.Equal(CommandKind.CreateTrade, restored.Operation.Kind);
        Assert.Equal(legacy, restored);
        Assert.DoesNotContain("operation", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("api/v1/groups/", serialized);
        Assert.Equal($"api/v2/communities/{Id}/trades", restored.CurrentShape().Path);
    }
}
