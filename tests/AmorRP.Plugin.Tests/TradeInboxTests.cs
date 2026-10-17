using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Trading;
using AmorRP.Plugin.Services.Api;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class TradeInboxTests
{
    private static readonly Guid Actor = Guid.NewGuid(), Peer = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static Character Person(Guid id) => new(id, "Character", "35", "Zalera", Now, "verified");
    private static Trade Invitation(Guid community, bool incoming = true) => new(Guid.NewGuid(), community, Guid.NewGuid(), "invited", 1,
        Now.AddMinutes(10), new(Person(incoming ? Peer : Actor), "0", [], false),
        new(Person(incoming ? Actor : Peer), "0", [], false), null, null);

    [Fact]
    public void Requests_from_other_joined_communities_are_visible_but_outgoing_and_unjoined_requests_are_not()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid(); var outside = Guid.NewGuid();
        var inbox = new TradeInbox(); inbox.Bind(Actor, [a, b, c]);
        inbox.Observe(a, Invitation(a)); inbox.Observe(b, Invitation(b));
        inbox.Observe(c, Invitation(c, incoming: false)); inbox.Observe(outside, Invitation(outside));
        Assert.Equal(2, inbox.Pending(Now).Length);
        Assert.Contains(inbox.Pending(Now), t => t.CommunityId == a);
        Assert.Contains(inbox.Pending(Now), t => t.CommunityId == b);
    }

    [Fact]
    public void Responding_or_expiry_clears_the_badge_and_a_late_snapshot_cannot_restore_an_old_invitation()
    {
        var community = Guid.NewGuid(); var inbox = new TradeInbox(); inbox.Bind(Actor, [community]);
        var invite = Invitation(community); inbox.Observe(community, invite);
        Assert.Single(inbox.Pending(Now));
        inbox.Observe(community, invite with { State = "open", Revision = 2 });
        inbox.Observe(community, invite);
        Assert.Empty(inbox.Pending(Now));
        inbox.Observe(community, invite with { State = "cancelled", Revision = 3 });
        inbox.Observe(community, invite with { Revision = 3 });
        Assert.Empty(inbox.Pending(Now));
        inbox.Observe(community, Invitation(community));
        Assert.Single(inbox.Pending(Now));
        Assert.Empty(inbox.Pending(Now.AddMinutes(10)));
    }

    [Fact]
    public void Membership_and_character_changes_remove_previous_scope_notifications()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var inbox = new TradeInbox(); inbox.Bind(Actor, [a, b]);
        inbox.Observe(a, Invitation(a)); inbox.Observe(b, Invitation(b));
        inbox.Bind(Actor, [a]); Assert.Single(inbox.Pending(Now));
        inbox.Bind(Peer, [a]); Assert.Empty(inbox.Pending(Now));
        inbox.Observe(a, Invitation(a)); Assert.Empty(inbox.Pending(Now));
        Assert.Throws<InvalidOperationException>(() => inbox.Observe(a, Invitation(b)));
        inbox.Clear(); inbox.Observe(a, Invitation(a, incoming: false)); Assert.Empty(inbox.Pending(Now));
    }
}
