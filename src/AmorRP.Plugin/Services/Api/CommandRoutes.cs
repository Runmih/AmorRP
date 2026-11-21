namespace AmorRP.Plugin.Services.Api;

internal enum CommandKind
{
    Unknown,
    ActivateSeason,
    ArchiveSeason,
    SetSeasonCreationPermission,
    CreateSeason,
    EditSeason,
    SetSeasonLogo,
    RemoveSeasonLogo,
    EditCurrency,
    SetCurrencyIcon,
    RemoveCurrencyIcon,
    AdjustCurrency,
    EditPolicy,
    ProposeOwnership,
    AcceptOwnership,
    CancelOwnership,
    EditMemberDescription,
    EditMemberPermissions,
    EditTradingPermission,
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
    EditGroupTrust,
    EditBillingPolicy,
    EditGroupBilling,
    SetGroupClockIn,
    CreateBill,
    DepositTreasury,
    WithdrawTreasury,
    CollectTreasuryReturn,
    SignOut,
}

internal enum CommandHandler { None, SeasonForm, Home, Balance, Policy, Member, Inventory, Trade, GroupProfile, GroupActivity, Treasury, Billing, BillingPolicy, GroupBilling, SignOut }

[Flags]
internal enum RefreshTargets
{
    None = 0, Season = 1, Inventory = 2, Members = 4, Policy = 8,
    Groups = 32, SeasonLogo = 64, CurrencyIcon = 128
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
        new("PUT", "api/v2/seasons/creation-permissions/{characterId}", new(CommandKind.SetSeasonCreationPermission, CommandHandler.None, RefreshTargets.Season)),
        new("POST", "api/v2/seasons/{seasonId}/activation", new(CommandKind.ActivateSeason, CommandHandler.None, RefreshTargets.Season)),
        new("POST", "api/v2/seasons/{seasonId}/archive", new(CommandKind.ArchiveSeason, CommandHandler.None, RefreshTargets.Season)),
        new("POST", "api/v2/seasons", new(CommandKind.CreateSeason, CommandHandler.SeasonForm, RefreshTargets.Season)),
        new("PATCH", "api/v2/seasons/{seasonId}", new(CommandKind.EditSeason, CommandHandler.Home, RefreshTargets.Season)),
        new("PUT", "api/v2/seasons/{seasonId}/logo", new(CommandKind.SetSeasonLogo, CommandHandler.Home, RefreshTargets.Season | RefreshTargets.SeasonLogo)),
        new("DELETE", "api/v2/seasons/{seasonId}/logo", new(CommandKind.RemoveSeasonLogo, CommandHandler.Home, RefreshTargets.Season | RefreshTargets.SeasonLogo)),
        new("PATCH", "api/v2/seasons/{seasonId}/currency", new(CommandKind.EditCurrency, CommandHandler.Home, RefreshTargets.Season)),
        new("PUT", "api/v2/seasons/{seasonId}/currency/icon", new(CommandKind.SetCurrencyIcon, CommandHandler.None, RefreshTargets.Season | RefreshTargets.CurrencyIcon)),
        new("DELETE", "api/v2/seasons/{seasonId}/currency/icon", new(CommandKind.RemoveCurrencyIcon, CommandHandler.None, RefreshTargets.Season | RefreshTargets.CurrencyIcon)),
        new("POST", "api/v2/seasons/{seasonId}/currency/adjustments", new(CommandKind.AdjustCurrency, CommandHandler.Balance, RefreshTargets.Season)),
        new("PUT", "api/v2/seasons/{seasonId}/policies", new(CommandKind.EditPolicy, CommandHandler.Policy, RefreshTargets.Season | RefreshTargets.Policy)),
        new("POST", "api/v2/seasons/{seasonId}/ownership-transfers", new(CommandKind.ProposeOwnership, CommandHandler.None, RefreshTargets.Season | RefreshTargets.Members)),
        new("POST", "api/v2/seasons/{seasonId}/ownership-transfers/{transferId}/accept", new(CommandKind.AcceptOwnership, CommandHandler.None, RefreshTargets.Season | RefreshTargets.Members)),
        new("POST", "api/v2/seasons/{seasonId}/ownership-transfers/{transferId}/cancel", new(CommandKind.CancelOwnership, CommandHandler.None, RefreshTargets.Season | RefreshTargets.Members)),
        new("PATCH", "api/v2/seasons/{seasonId}/members/{characterId}/description", new(CommandKind.EditMemberDescription, CommandHandler.Member, RefreshTargets.Season | RefreshTargets.Members)),
        new("PUT", "api/v2/seasons/{seasonId}/members/{characterId}/capabilities", new(CommandKind.EditMemberPermissions, CommandHandler.Member, RefreshTargets.Season | RefreshTargets.Members)),
        new("PUT", "api/v2/seasons/{seasonId}/members/{characterId}/trade-restriction", new(CommandKind.EditTradingPermission, CommandHandler.Member, RefreshTargets.Season | RefreshTargets.Members)),
        new("POST", "api/v2/seasons/{seasonId}/announcements", new(CommandKind.PublishAnnouncement, CommandHandler.Home, RefreshTargets.Season)),
        new("PUT", "api/v2/seasons/{seasonId}/announcements/{announcementId}/read", new(CommandKind.ReadAnnouncements, CommandHandler.None, RefreshTargets.None)),
        new("POST", "api/v2/seasons/{seasonId}/item-definitions", new(CommandKind.CreateRecipe, CommandHandler.Inventory, RefreshTargets.Season | RefreshTargets.Inventory)),
        new("PATCH", "api/v2/seasons/{seasonId}/item-definitions/{definitionId}", new(CommandKind.EditRecipe, CommandHandler.Inventory, RefreshTargets.Season | RefreshTargets.Inventory)),
        new("POST", "api/v2/seasons/{seasonId}/item-definitions/{definitionId}/retirement", new(CommandKind.RetireRecipe, CommandHandler.Inventory, RefreshTargets.Season | RefreshTargets.Inventory)),
        new("POST", "api/v2/seasons/{seasonId}/inventory/potions", new(CommandKind.CraftConsumables, CommandHandler.Inventory, RefreshTargets.Season | RefreshTargets.Inventory)),
        new("POST", "api/v2/seasons/{seasonId}/inventory/letters", new(CommandKind.CreateWriting, CommandHandler.Inventory, RefreshTargets.Season | RefreshTargets.Inventory)),
        new("PATCH", "api/v2/seasons/{seasonId}/letters/{letterId}", new(CommandKind.EditWriting, CommandHandler.Inventory, RefreshTargets.Season | RefreshTargets.Inventory)),
        new("PATCH", "api/v2/seasons/{seasonId}/members/{characterId}/holdings/{holdingId}", new(CommandKind.ChangeHolding, CommandHandler.Inventory, RefreshTargets.Season | RefreshTargets.Inventory)),
        new("DELETE", "api/v2/seasons/{seasonId}/members/{characterId}/holdings/{holdingId}", new(CommandKind.DeleteHolding, CommandHandler.Inventory, RefreshTargets.Season | RefreshTargets.Inventory)),
        new("POST", "api/v2/seasons/{seasonId}/inventory/{holdingId}/discard", new(CommandKind.DiscardHolding, CommandHandler.Inventory, RefreshTargets.Season | RefreshTargets.Inventory)),
        new("POST", "api/v2/seasons/{seasonId}/inventory/removals", new(CommandKind.RemoveInventory, CommandHandler.Inventory, RefreshTargets.Season | RefreshTargets.Inventory)),
        new("POST", "api/v2/seasons/{seasonId}/inventory/{holdingId}/use", new(CommandKind.UseItem, CommandHandler.Inventory, RefreshTargets.Season | RefreshTargets.Inventory)),
        new("POST", "api/v2/seasons/{seasonId}/trades", new(CommandKind.CreateTrade, CommandHandler.Trade, RefreshTargets.Season | RefreshTargets.Inventory)),
        new("POST", "api/v2/seasons/{seasonId}/trades/{tradeId}/accept", new(CommandKind.AcceptTrade, CommandHandler.Trade, RefreshTargets.Season | RefreshTargets.Inventory)),
        new("PUT", "api/v2/seasons/{seasonId}/trades/{tradeId}/offers/mine", new(CommandKind.EditTradeOffer, CommandHandler.Trade, RefreshTargets.Season | RefreshTargets.Inventory)),
        new("POST", "api/v2/seasons/{seasonId}/trades/{tradeId}/confirm", new(CommandKind.ConfirmTrade, CommandHandler.Trade, RefreshTargets.Season | RefreshTargets.Inventory)),
        new("POST", "api/v2/seasons/{seasonId}/trades/{tradeId}/final-confirm", new(CommandKind.FinalConfirmTrade, CommandHandler.Trade, RefreshTargets.Season | RefreshTargets.Inventory)),
        new("POST", "api/v2/seasons/{seasonId}/trades/{tradeId}/cancel", new(CommandKind.CancelTrade, CommandHandler.Trade, RefreshTargets.Season | RefreshTargets.Inventory)),
        new("PUT", "api/v2/seasons/{seasonId}/groups/{groupId}/features", new(CommandKind.EditGroupFeatures, CommandHandler.GroupProfile, RefreshTargets.Groups)),
        new("PUT", "api/v2/seasons/{seasonId}/groups/{groupId}/members/{characterId}/capabilities", new(CommandKind.EditGroupStaff, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("POST", "api/v2/seasons/{seasonId}/groups", new(CommandKind.CreateGroup, CommandHandler.GroupProfile, RefreshTargets.Groups)),
        new("PATCH", "api/v2/seasons/{seasonId}/groups/{groupId}", new(CommandKind.EditGroup, CommandHandler.GroupProfile, RefreshTargets.Groups)),
        new("DELETE", "api/v2/seasons/{seasonId}/groups/{groupId}", new(CommandKind.DeleteGroup, CommandHandler.GroupProfile, RefreshTargets.Groups | RefreshTargets.Season)),
        new("PUT", "api/v2/seasons/{seasonId}/groups/{groupId}/logo", new(CommandKind.SetGroupLogo, CommandHandler.GroupProfile, RefreshTargets.Groups)),
        new("DELETE", "api/v2/seasons/{seasonId}/groups/{groupId}/logo", new(CommandKind.RemoveGroupLogo, CommandHandler.GroupProfile, RefreshTargets.Groups)),
        new("POST", "api/v2/seasons/{seasonId}/groups/{groupId}/join", new(CommandKind.JoinGroup, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("DELETE", "api/v2/seasons/{seasonId}/groups/{groupId}/membership", new(CommandKind.LeaveGroup, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("PUT", "api/v2/seasons/{seasonId}/groups/{groupId}/favorite", new(CommandKind.FavoriteGroup, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("DELETE", "api/v2/seasons/{seasonId}/groups/{groupId}/favorite", new(CommandKind.UnfavoriteGroup, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("PUT", "api/v2/seasons/{seasonId}/groups/{groupId}/notice", new(CommandKind.SaveGroupNotice, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("DELETE", "api/v2/seasons/{seasonId}/groups/{groupId}/notice", new(CommandKind.DeleteGroupNotice, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("POST", "api/v2/seasons/{seasonId}/groups/{groupId}/events", new(CommandKind.CreateGroupEvent, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("PUT", "api/v2/seasons/{seasonId}/groups/{groupId}/events/{eventId}", new(CommandKind.EditGroupEvent, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("DELETE", "api/v2/seasons/{seasonId}/groups/{groupId}/events/{eventId}", new(CommandKind.DeleteGroupEvent, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("POST", "api/v2/seasons/{seasonId}/groups/{groupId}/announcements", new(CommandKind.PublishGroupAnnouncement, CommandHandler.GroupActivity, RefreshTargets.Groups)),
        new("PUT", "api/v2/seasons/{seasonId}/groups/{groupId}/trust", new(CommandKind.EditGroupTrust, CommandHandler.GroupProfile, RefreshTargets.Groups)),
        new("PUT", "api/v2/seasons/{seasonId}/billing-policy", new(CommandKind.EditBillingPolicy, CommandHandler.BillingPolicy, RefreshTargets.Season)),
        new("PUT", "api/v2/seasons/{seasonId}/groups/{groupId}/billing-settings", new(CommandKind.EditGroupBilling, CommandHandler.GroupBilling, RefreshTargets.None)),
        new("PUT", "api/v2/seasons/{seasonId}/groups/{groupId}/clock-in", new(CommandKind.SetGroupClockIn, CommandHandler.GroupBilling, RefreshTargets.None)),
        new("POST", "api/v2/seasons/{seasonId}/groups/{groupId}/bills", new(CommandKind.CreateBill, CommandHandler.Billing, RefreshTargets.Season)),
        new("POST", "api/v2/seasons/{seasonId}/groups/{groupId}/treasury/deposits", new(CommandKind.DepositTreasury, CommandHandler.Treasury, RefreshTargets.Season)),
        new("POST", "api/v2/seasons/{seasonId}/groups/{groupId}/treasury/withdrawals", new(CommandKind.WithdrawTreasury, CommandHandler.Treasury, RefreshTargets.Season)),
        new("POST", "api/v2/seasons/{seasonId}/groups/{groupId}/treasury/return", new(CommandKind.CollectTreasuryReturn, CommandHandler.Treasury, RefreshTargets.Season)),
        new("DELETE", "api/v2/auth/sessions/current", new(CommandKind.SignOut, CommandHandler.SignOut, RefreshTargets.None)),
    };
    private static readonly CommandOperation Unknown = new(CommandKind.Unknown, CommandHandler.None, RefreshTargets.Season);
    public static CommandOperation Resolve(CommandRequest request)
    {
        var segments = request.Path.Split('/');
        return All.FirstOrDefault(x => x.Matches(request.Method, segments))?.Operation ?? Unknown;
    }
}
