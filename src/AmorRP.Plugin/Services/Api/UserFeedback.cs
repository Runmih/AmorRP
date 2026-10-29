using System.Net;
namespace AmorRP.Plugin.Services.Api;

public static class UserFeedback
{
    public static string Error(HttpStatusCode status, string code, string serverMessage) => code switch
    {
        "group_capability_required" => "You do not have permission for this group action.",
        "group_feature_disabled" => "The community owner has not enabled that group feature.",
        "invalid_group_capability" => "Choose only the available group permissions.",
        "group_owner_protected" => "Group owner permissions are automatic.",
        "group_owner_required" => "Only this group’s owner can edit its contents.",
        "group_membership_required" => "Join this group to read its members and announcements.",
        "group_closed" => "This group is closed to new members.",
        "group_password_invalid" => "That group password is incorrect. Check it and try again.",
        "owned_groups_limit" => "You already own 3 groups. Delete one before creating another.",
        "invalid_event_range" => "Check the event dates and Server Time. The end must be after the start.",
        "authentication_required" or "refresh_reused" or "refresh_superseded" => "Your login has expired. Sign in again to continue.",
        "capability_required" or "owner_required" => "You do not have permission for this action. Ask your community owner for access.",
        "membership_inactive" or "membership_blocked" => "You no longer have access to this community. Contact its owner if you need help.",
        "version_mismatch" or "precondition_required" or "holding_changed" or "definition_changed" => "This information changed since you opened it. Refresh, check the latest values, and try again.",
        "quota_exhausted" => "You have used your weekly crafting limit. You can craft again after the next reset.",
        "insufficient_available_quantity" => "You do not have enough available items or currency. Some may already be reserved for another trade.",
        "invitation_unavailable" => "This invitation is no longer available. Ask the community owner for a new code.",
        "already_joined" => "You are already a member of this community. Choose it from the community list.",
        "trading_restricted" => "Trading is disabled for one of these characters. Ask the community owner to check Trading permissions.",
        "trade_changed" => "The trade changed. Review the latest offer before accepting it.",
        "trade_busy" => "One of you already has a trade in progress. Finish or cancel it before starting another.",
        "trade_closed" or "trade_not_open" or "trade_not_invited" => "This trade is no longer available. Start a new trade if you still want to exchange items.",
        "trade_already_confirmed" => "You have already confirmed this trade. Waiting for the other player.",
        "letter_locked" => "This written item has been traded and can no longer be edited.",
        "operation_replay_expired" => "This saved action is too old to retry safely. Check your items and balance before trying a new action.",
        "idempotency_mismatch" => "This saved action could not be matched safely. Keep its details and contact support.",
        "login_expired" => "This login attempt expired. Start login again.",
        "login_not_ready" => "Finish verifying in your browser before entering the pairing code.",
        "login_pairing_invalid" => "The pairing code does not match. Check the code shown in your browser and try again.",
        "login_pairing_locked" => "Too many incorrect pairing codes. Start a new login attempt.",
        "rate_limited" => "Too many requests. Please wait a minute before trying again.",
        "identity_provider_unavailable" or "authentication_unavailable" => "XIVAuth is temporarily unavailable. Please try signing in again shortly.",
        "not_found" => "This community, member or item is no longer available. Refresh and select it again.",
        _ => (int)status switch
        {
            401 => "Your login has expired. Sign in again to continue.",
            403 => "You do not have permission for this action.",
            404 => "This feature or item is unavailable. Refresh and try again. If it continues, check that the server is up to date.",
            429 => "Too many requests. Please wait a minute before trying again.",
            >= 500 => "Amor RP is temporarily unavailable or starting up. Please wait a moment; your current information has been kept.",
            // Validation messages are written for users by the server. Never display a bare machine code.
            _ when serverMessage.Length > 0 && serverMessage.Contains(' ') && !serverMessage.Contains("Exception", StringComparison.OrdinalIgnoreCase) => serverMessage,
            _ => "This action could not be completed. Check the details or refresh and try again."
        }
    };

    public static string Success(CommandRequest request) => request.Operation.Kind switch
    {
        CommandKind.CreateCommunity => "Community created.",
        CommandKind.JoinCommunity => "You joined the community.",
        CommandKind.LeaveCommunity or CommandKind.RemoveMember => "Membership ended. Items and currency have been kept.",
        CommandKind.DeleteCommunity => "Community deleted.",
        CommandKind.EditCommunity => "Community profile saved.",
        CommandKind.EditCurrency => "Currency settings saved.",
        CommandKind.AdjustCurrency => "Currency balance updated.",
        CommandKind.SetCommunityLogo => "Community logo updated.",
        CommandKind.RemoveCommunityLogo => "Community logo removed.",
        CommandKind.SetCurrencyIcon => "Currency icon updated.",
        CommandKind.RemoveCurrencyIcon => "Currency icon reset.",
        CommandKind.EditMemberDescription => "Character description saved.",
        CommandKind.EditMemberPermissions => "Member permissions saved.",
        CommandKind.EditTradingPermission => "Trading permission saved.",
        CommandKind.BlockMember => "Member blocked from rejoining.",
        CommandKind.UnblockMember => "Member can rejoin with an invitation.",
        CommandKind.EditPolicy => "Weekly crafting limits saved.",
        CommandKind.CreateInvitation => "Invitation created. Share the code below to invite someone.",
        CommandKind.DeleteInvitation => "Invitation deleted. Its code can no longer be used.",
        CommandKind.ProposeOwnership => "Ownership transfer proposed.",
        CommandKind.AcceptOwnership => "Community ownership transferred.",
        CommandKind.CancelOwnership => "Ownership transfer cancelled.",
        CommandKind.PublishAnnouncement => "Announcement published.",
        CommandKind.CreateRecipe or CommandKind.EditRecipe or CommandKind.RetireRecipe => "Recipe saved.",
        CommandKind.CraftConsumables => "Items crafted and added to your inventory.",
        CommandKind.CreateWriting => "Written item created in your inventory.",
        CommandKind.EditWriting => "Written item saved.",
        CommandKind.ChangeHolding => "Item quantity updated.",
        CommandKind.DeleteHolding or CommandKind.RemoveInventory => "Items removed from the member's inventory.",
        CommandKind.DiscardHolding => "Items discarded. Weekly crafting points have not been refunded.",
        CommandKind.CreateTrade => "Trade request sent. Waiting for the other player to accept.",
        CommandKind.AcceptTrade => "Trade request accepted. Build your offer below.",
        CommandKind.EditTradeOffer => "Your trade offer was saved.",
        CommandKind.ConfirmTrade => "Offer accepted. Both players must complete the final review.",
        CommandKind.FinalConfirmTrade => "Final confirmation saved. Waiting for the other player.",
        CommandKind.CancelTrade => "Trade cancelled. Your items and currency have been released.",
        CommandKind.EditGroupFeatures => "Group features saved.",
        CommandKind.EditGroupStaff => "Group staff permissions saved.",
        CommandKind.CreateGroup => "Group created.",
        CommandKind.EditGroup => "Group profile saved.",
        CommandKind.DeleteGroup => "Group deleted.",
        CommandKind.SetGroupLogo => "Group logo updated.",
        CommandKind.RemoveGroupLogo => "Group logo removed.",
        CommandKind.JoinGroup => "Joined group.",
        CommandKind.LeaveGroup => "Left group.",
        CommandKind.FavoriteGroup => "Group added to favorites.",
        CommandKind.UnfavoriteGroup => "Group removed from favorites.",
        CommandKind.SaveGroupNotice => "Notice published.",
        CommandKind.DeleteGroupNotice => "Notice removed.",
        CommandKind.CreateGroupEvent or CommandKind.EditGroupEvent => "Event saved.",
        CommandKind.DeleteGroupEvent => "Event cancelled.",
        CommandKind.PublishGroupAnnouncement => "Group announcement published.",
        _ => "Changes saved."
    };
    public static string Details(Exception? error) => error is FoundationApiException api
        ? $"HTTP {(int)api.Status}; code: {api.Code}; request: {api.RequestId ?? "unavailable"}.\nServer: {api.ServerMessage}"
        : error is null ? "No additional details available." : $"Connection error: {error.GetType().Name}.";
}
