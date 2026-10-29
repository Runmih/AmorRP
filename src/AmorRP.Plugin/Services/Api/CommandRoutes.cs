namespace AmorRP.Plugin.Services.Api;

internal enum CommandKind
{
    Unknown,
    CreateCommunity,
    JoinCommunity,
    EditCommunity,
    LeaveCommunity,
    DeleteCommunity,
    SetCommunityLogo,
    RemoveCommunityLogo,
    EditCurrency,
    SetCurrencyIcon,
    RemoveCurrencyIcon,
    AdjustCurrency,
    EditPolicy,
    CreateInvitation,
    DeleteInvitation,
    ProposeOwnership,
    AcceptOwnership,
    CancelOwnership,
    EditMemberDescription,
    EditMemberPermissions,
    EditTradingPermission,
    RemoveMember,
    BlockMember,
    UnblockMember,
    PublishAnnouncement,
    ReadAnnouncements,
    CreateRecipe,
    EditRecipe,
    RetireRecipe,
    CraftConsumables,
    CreateWriting,
    EditWriting,
    ChangeHolding,
    DeleteHolding,
    DiscardHolding,
    RemoveInventory,
    UseItem,
    CreateTrade,
    AcceptTrade,
    EditTradeOffer,
    ConfirmTrade,
    FinalConfirmTrade,
    CancelTrade,
    CreateGroup,
    EditGroup,
    DeleteGroup,
    SetGroupLogo,
    RemoveGroupLogo,
    JoinGroup,
    LeaveGroup,
    FavoriteGroup,
    UnfavoriteGroup,
    SaveGroupNotice,
    DeleteGroupNotice,
    CreateGroupEvent,
    EditGroupEvent,
    DeleteGroupEvent,
    PublishGroupAnnouncement,
    EditGroupFeatures,
    EditGroupStaff,
    DepositTreasury,
    WithdrawTreasury,
    CollectTreasuryReturn,
    SignOut,
}

internal enum CommandHandler { None, CommunityForm, Home, Balance, Policy, Invitation, Member, Inventory, Trade, GroupProfile, GroupActivity, Treasury, SignOut }

[Flags]
internal enum RefreshTargets
{
    None = 0, Community = 1, Inventory = 2, Members = 4, Policy = 8,
    Invitations = 16, Groups = 32, CommunityLogo = 64, CurrencyIcon = 128
}

internal sealed record CommandOperation(CommandKind Kind, CommandHandler Handler, RefreshTargets Refresh);

// Exact route shapes, HTTP verbs and handlers live in one place. This also
// handles restored pending requests without rewriting their saved wire identity.
internal static class CommandRoutes
{
    internal sealed record Route(string Method, string Template, CommandOperation Operation)
    {
        private readonly string[] segments = Template.Split('/');
        public bool Matches(string method, string[] path)
        {
            if (Method != method || segments.Length != path.Length) return false;
            for (var i = 0; i < segments.Length; i++)
                if (segments[i].StartsWith('{') ? !Guid.TryParse(path[i], out _) : segments[i] != path[i]) return false;
            return true;
        }
    }

    internal static readonly IReadOnlyList<Route> All = new Route[]
    {
        new("POST", "api/v2/communities", new(CommandKind.CreateCommunity, CommandHandler.CommunityForm, RefreshTargets.Community)),
        new("POST", "api/v2/communities/join", new(CommandKind.JoinCommunity, CommandHandler.CommunityForm, RefreshTargets.Community)),
        new("PATCH", "api/v2/communities/{communityId}", new(CommandKind.EditCommunity, CommandHandler.Home, RefreshTargets.Community)),
        new("POST", "api/v2/communities/{communityId}/leave", new(CommandKind.LeaveCommunity, CommandHandler.None, RefreshTargets.Community)),
        new("POST", "api/v2/communities/{communityId}/deletion", new(CommandKind.DeleteCommunity, CommandHandler.None, RefreshTargets.Community)),
        new("PUT", "api/v2/communities/{communityId}/logo", new(CommandKind.SetCommunityLogo, CommandHandler.Home, RefreshTargets.Community | RefreshTargets.CommunityLogo)),
        new("DELETE", "api/v2/communities/{communityId}/logo", new(CommandKind.RemoveCommunityLogo, CommandHandler.Home, RefreshTargets.Community | RefreshTargets.CommunityLogo)),
        new("PATCH", "api/v2/communities/{communityId}/currency", new(CommandKind.EditCurrency, CommandHandler.Home, RefreshTargets.Community)),
        new("PUT", "api/v2/communities/{communityId}/currency/icon", new(CommandKind.SetCurrencyIcon, CommandHandler.None, RefreshTargets.Community | RefreshTargets.CurrencyIcon)),
        new("DELETE", "api/v2/communities/{communityId}/currency/icon", new(CommandKind.RemoveCurrencyIcon, CommandHandler.None, RefreshTargets.Community | RefreshTargets.CurrencyIcon)),
        new("POST", "api/v2/communities/{communityId}/currency/adjustments", new(CommandKind.AdjustCurrency, CommandHandler.Balance, RefreshTargets.Community)),
        new("PUT", "api/v2/communities/{communityId}/policies", new(CommandKind.EditPolicy, CommandHandler.Policy, RefreshTargets.Community | RefreshTargets.Policy)),
        new("POST", "api/v2/communities/{communityId}/invitations", new(CommandKind.CreateInvitation, CommandHandler.Invitation, RefreshTargets.Community | RefreshTargets.Invitations)),
        new("DELETE", "api/v2/communities/{communityId}/invitations/{invitationId}", new(CommandKind.DeleteInvitation, CommandHandler.None, RefreshTargets.Community | RefreshTargets.Invitations)),
        new("POST", "api/v2/communities/{communityId}/ownership-transfers", new(CommandKind.ProposeOwnership, CommandHandler.None, RefreshTargets.Community | RefreshTargets.Members)),
        new("POST", "api/v2/communities/{communityId}/ownership-transfers/{transferId}/accept", new(CommandKind.AcceptOwnership, CommandHandler.None, RefreshTargets.Community | RefreshTargets.Members)),
        new("POST", "api/v2/communities/{communityId}/ownership-transfers/{transferId}/cancel", new(CommandKind.CancelOwnership, CommandHandler.None, RefreshTargets.Community | RefreshTargets.Members)),
        new("PATCH", "api/v2/communities/{communityId}/members/{characterId}/description", new(CommandKind.EditMemberDescription, CommandHandler.Member, RefreshTargets.Community | RefreshTargets.Members)),
        new("PUT", "api/v2/communities/{communityId}/members/{characterId}/capabilities", new(CommandKind.EditMemberPermissions, CommandHandler.Member, RefreshTargets.Community | RefreshTargets.Members)),
        new("PUT", "api/v2/communities/{communityId}/members/{characterId}/trade-restriction", new(CommandKind.EditTradingPermission, CommandHandler.Member, RefreshTargets.Community | RefreshTargets.Members)),
        new("DELETE", "api/v2/communities/{communityId}/members/{characterId}", new(CommandKind.RemoveMember, CommandHandler.Member, RefreshTargets.Community | RefreshTargets.Members)),
        new("PUT", "api/v2/communities/{communityId}/members/{characterId}/block", new(CommandKind.BlockMember, CommandHandler.Member, RefreshTargets.Community | RefreshTargets.Members)),
        new("DELETE", "api/v2/communities/{communityId}/members/{characterId}/block", new(CommandKind.UnblockMember, CommandHandler.Member, RefreshTargets.Community | RefreshTargets.Members)),
        new("POST", "api/v2/communities/{communityId}/members/{characterId}/removal", new(CommandKind.RemoveMember, CommandHandler.Member, RefreshTargets.Community | RefreshTargets.Members)),
        new("POST", "api/v2/communities/{communityId}/members/{characterId}/restoration", new(CommandKind.UnblockMember, CommandHandler.Member, RefreshTargets.Community | RefreshTargets.Members)),
        new("POST", "api/v2/communities/{communityId}/announcements", new(CommandKind.PublishAnnouncement, CommandHandler.Home, RefreshTargets.Community)),
        new("PUT", "api/v2/communities/{communityId}/announcements/{announcementId}/read", new(CommandKind.ReadAnnouncements, CommandHandler.None, RefreshTargets.None)),
        new("POST", "api/v2/communities/{communityId}/item-definitions", new(CommandKind.CreateRecipe, CommandHandler.Inventory, RefreshTargets.Community | RefreshTargets.Inventory)),
        new("PATCH", "api/v2/communities/{communityId}/item-definitions/{definitionId}", new(CommandKind.EditRecipe, CommandHandler.Inventory, RefreshTargets.Community | RefreshTargets.Inventory)),
        new("POST", "api/v2/communities/{communityId}/item-definitions/{definitionId}/retirement", new(CommandKind.RetireRecipe, CommandHandler.Inventory, RefreshTargets.Community | RefreshTargets.Inventory)),
        new("POST", "api/v2/communities/{communityId}/inventory/potions", new(CommandKind.CraftConsumables, CommandHandler.Inventory, RefreshTargets.Community | RefreshTargets.Inventory)),
        new("POST", "api/v2/communities/{communityId}/inventory/letters", new(CommandKind.CreateWriting, CommandHandler.Inventory, RefreshTargets.Community | RefreshTargets.Inventory)),
        new("PATCH", "api/v2/communities/{communityId}/letters/{letterId}", new(CommandKind.EditWriting, CommandHandler.Inventory, RefreshTargets.Community | RefreshTargets.Inventory)),
        new("PATCH", "api/v2/communities/{communityId}/members/{characterId}/holdings/{holdingId}", new(CommandKind.ChangeHolding, CommandHandler.Inventory, RefreshTargets.Community | RefreshTargets.Inventory)),
        new("DELETE", "api/v2/communities/{communityId}/members/{characterId}/holdings/{holdingId}", new(CommandKind.DeleteHolding, CommandHandler.Inventory, RefreshTargets.Community | RefreshTargets.Inventory)),
        new("POST", "api/v2/communities/{communityId}/inventory/{holdingId}/discard", new(CommandKind.DiscardHolding, CommandHandler.Inventory, RefreshTargets.Community | RefreshTargets.Inventory)),
        new("POST", "api/v2/communities/{communityId}/inventory/removals", new(CommandKind.RemoveInventory, CommandHandler.Inventory, RefreshTargets.Community | RefreshTargets.Inventory)),
        new("POST", "api/v2/communities/{communityId}/inventory/{holdingId}/use", new(CommandKind.UseItem, CommandHandler.Inventory, RefreshTargets.Community | RefreshTargets.Inventory)),
        new("POST", "api/v2/communities/{communityId}/trades", new(CommandKind.CreateTrade, CommandHandler.Trade, RefreshTargets.Community | RefreshTargets.Inventory)),
        new("POST", "api/v2/communities/{communityId}/trades/{tradeId}/accept", new(CommandKind.AcceptTrade, CommandHandler.Trade, RefreshTargets.Community | RefreshTargets.Inventory)),
        new("PUT", "api/v2/communities/{communityId}/trades/{tradeId}/offers/mine", new(CommandKind.EditTradeOffer, CommandHandler.Trade, RefreshTargets.Community | RefreshTargets.Inventory)),
        new("POST", "api/v2/communities/{communityId}/trades/{tradeId}/confirm", new(CommandKind.ConfirmTrade, CommandHandler.Trade, RefreshTargets.Community | RefreshTargets.Inventory)),
        new("POST", "api/v2/communities/{communityId}/trades/{tradeId}/final-confirm", new(CommandKind.FinalConfirmTrade, CommandHandler.Trade, RefreshTargets.Community | RefreshTargets.Inventory)),
        new("POST", "api/v2/communities/{communityId}/trades/{tradeId}/cancel", new(CommandKind.CancelTrade, CommandHandler.Trade, RefreshTargets.Community | RefreshTargets.Inventory)),
        new("PUT", "api/v2/communities/{communityId}/groups/{groupId}/features", new(CommandKind.EditGroupFeatures, CommandHandler.GroupProfile, RefreshTargets.Groups)),
        new("PUT", "api/v2/communities/{communityId}/groups/{groupId}/members/{characterId}/capabilities", new(CommandKind.EditGroupStaff, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("POST", "api/v2/communities/{communityId}/groups", new(CommandKind.CreateGroup, CommandHandler.GroupProfile, RefreshTargets.Groups)),
        new("PATCH", "api/v2/communities/{communityId}/groups/{groupId}", new(CommandKind.EditGroup, CommandHandler.GroupProfile, RefreshTargets.Groups)),
        new("DELETE", "api/v2/communities/{communityId}/groups/{groupId}", new(CommandKind.DeleteGroup, CommandHandler.GroupProfile, RefreshTargets.Groups | RefreshTargets.Community)),
        new("PUT", "api/v2/communities/{communityId}/groups/{groupId}/logo", new(CommandKind.SetGroupLogo, CommandHandler.GroupProfile, RefreshTargets.Groups)),
        new("DELETE", "api/v2/communities/{communityId}/groups/{groupId}/logo", new(CommandKind.RemoveGroupLogo, CommandHandler.GroupProfile, RefreshTargets.Groups)),
        new("POST", "api/v2/communities/{communityId}/groups/{groupId}/join", new(CommandKind.JoinGroup, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("DELETE", "api/v2/communities/{communityId}/groups/{groupId}/membership", new(CommandKind.LeaveGroup, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("PUT", "api/v2/communities/{communityId}/groups/{groupId}/favorite", new(CommandKind.FavoriteGroup, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("DELETE", "api/v2/communities/{communityId}/groups/{groupId}/favorite", new(CommandKind.UnfavoriteGroup, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("PUT", "api/v2/communities/{communityId}/groups/{groupId}/notice", new(CommandKind.SaveGroupNotice, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("DELETE", "api/v2/communities/{communityId}/groups/{groupId}/notice", new(CommandKind.DeleteGroupNotice, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("POST", "api/v2/communities/{communityId}/groups/{groupId}/events", new(CommandKind.CreateGroupEvent, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("PUT", "api/v2/communities/{communityId}/groups/{groupId}/events/{eventId}", new(CommandKind.EditGroupEvent, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("DELETE", "api/v2/communities/{communityId}/groups/{groupId}/events/{eventId}", new(CommandKind.DeleteGroupEvent, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("POST", "api/v2/communities/{communityId}/groups/{groupId}/announcements", new(CommandKind.PublishGroupAnnouncement, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("POST", "api/v2/communities/{communityId}/groups/{groupId}/treasury/deposits", new(CommandKind.DepositTreasury, CommandHandler.Treasury, RefreshTargets.Community)),
        new("POST", "api/v2/communities/{communityId}/groups/{groupId}/treasury/withdrawals", new(CommandKind.WithdrawTreasury, CommandHandler.Treasury, RefreshTargets.Community)),
        new("POST", "api/v2/communities/{communityId}/groups/{groupId}/treasury/return", new(CommandKind.CollectTreasuryReturn, CommandHandler.Treasury, RefreshTargets.Community)),
        new("DELETE", "api/v2/auth/sessions/current", new(CommandKind.SignOut, CommandHandler.SignOut, RefreshTargets.None)),
    };
    private static readonly CommandOperation Unknown = new(CommandKind.Unknown, CommandHandler.None, RefreshTargets.Community);
    public static CommandOperation Resolve(CommandRequest request)
    {
        var current = request.CurrentShape();
        var segments = current.Path.Split('/');
        return All.FirstOrDefault(x => x.Matches(current.Method, segments))?.Operation ?? Unknown;
    }
}
