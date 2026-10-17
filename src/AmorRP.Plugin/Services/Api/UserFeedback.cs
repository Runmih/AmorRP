using System.Net;
namespace AmorRP.Plugin.Services.Api;

public static class UserFeedback
{
    public static string Error(HttpStatusCode status, string code, string serverMessage) => code switch
    {
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

    public static string Success(CommandRequest request)
    {
        var p = request.Path;
        if (p.Contains("/groups", StringComparison.Ordinal))
        {
            if (p.EndsWith("/join", StringComparison.Ordinal)) return "Joined group.";
            if (p.EndsWith("/membership", StringComparison.Ordinal)) return "Left group.";
            if (p.EndsWith("/favorite", StringComparison.Ordinal)) return request.Method == "DELETE" ? "Group removed from favorites." : "Group added to favorites.";
            if (p.EndsWith("/notice", StringComparison.Ordinal)) return request.Method == "DELETE" ? "Notice removed." : "Notice published.";
            if (p.EndsWith("/announcements", StringComparison.Ordinal)) return "Group announcement published.";
            if (p.Contains("/events", StringComparison.Ordinal)) return request.Method == "DELETE" ? "Event cancelled." : "Event saved.";
            if (p.EndsWith("/logo", StringComparison.Ordinal)) return request.Method == "DELETE" ? "Group logo removed." : "Group logo updated.";
            return request.Method == "DELETE" ? "Group deleted." : request.Method == "POST" ? "Group created." : "Group profile saved.";
        }

        if (p.EndsWith("/currency/adjustments", StringComparison.Ordinal)) return "Currency balance updated.";
        if (p.EndsWith("/description", StringComparison.Ordinal)) return "Character description saved.";
        if (p.EndsWith("/capabilities", StringComparison.Ordinal)) return "Member permissions saved.";
        if (p.EndsWith("/block", StringComparison.Ordinal)) return request.Method == "DELETE" ? "Member can rejoin with an invitation." : "Member blocked from rejoining.";
        if (p.Contains("/members/", StringComparison.Ordinal) && !p.Contains("/holdings/", StringComparison.Ordinal) && request.Method == "DELETE") return "Membership ended. Items and currency have been kept.";
        if (p.Contains("/holdings/", StringComparison.Ordinal)) return request.Method == "DELETE" ? "Items removed from the member's inventory." : "Item quantity updated.";
        if (p.EndsWith("/discard", StringComparison.Ordinal)) return "Items discarded. Weekly crafting points have not been refunded.";
        if (p.EndsWith("/inventory/removals", StringComparison.Ordinal)) return "Items removed from the member's inventory.";
        if (p.Contains("/invitations/", StringComparison.Ordinal)) return "Invitation deleted. Its code can no longer be used.";
        if (p.EndsWith("/invitations", StringComparison.Ordinal)) return "Invitation created. Share the code below to invite someone.";
        if (p.EndsWith("/policies", StringComparison.Ordinal)) return "Weekly crafting limits saved.";
        if (p.EndsWith("/deletion", StringComparison.Ordinal)) return "Community deleted.";
        if (p.Contains("/ownership-transfers", StringComparison.Ordinal)) return p.EndsWith("/accept", StringComparison.Ordinal) ? "Community ownership transferred." : p.EndsWith("/cancel", StringComparison.Ordinal) ? "Ownership transfer cancelled." : "Ownership transfer proposed.";
        if (p.EndsWith("/logo", StringComparison.Ordinal)) return request.Method == "DELETE" ? "Community logo removed." : "Community logo updated.";
        if (p.Contains("/currency/icon", StringComparison.Ordinal)) return request.Method == "DELETE" ? "Currency icon reset." : "Currency icon updated.";
        if (p.EndsWith("/currency", StringComparison.Ordinal)) return "Currency settings saved.";
        if (p.Contains("/item-definitions", StringComparison.Ordinal)) return "Potion recipe saved.";
        if (p.Contains("/letters", StringComparison.Ordinal)) return request.Method == "POST" ? "Written item created in your inventory." : "Written item saved.";
        if (p.EndsWith("/inventory/potions", StringComparison.Ordinal)) return "Potions crafted and added to your inventory.";
        if (p.EndsWith("/inventory", StringComparison.Ordinal)) return "Items added to your inventory.";
        if (p == "api/v2/communities/join") return "You joined the community.";
        if (p == "api/v2/communities") return "Community created.";
        if (p.Contains("/trades", StringComparison.Ordinal))
        {
            if (p.EndsWith("/trades", StringComparison.Ordinal)) return "Trade request sent. Waiting for the other player to accept.";
            if (p.EndsWith("/accept", StringComparison.Ordinal)) return "Trade request accepted. Build your offer below.";
            if (p.EndsWith("/offers/mine", StringComparison.Ordinal)) return "Your trade offer was saved.";
            if (p.EndsWith("/final-confirm", StringComparison.Ordinal)) return "Final confirmation saved. Waiting for the other player.";
            if (p.EndsWith("/confirm", StringComparison.Ordinal)) return "Offer accepted. Both players must complete the final review.";
            if (p.EndsWith("/cancel", StringComparison.Ordinal)) return "Trade cancelled. Your items and currency have been released.";
        }
        return "Changes saved.";
    }
    public static string Details(Exception? error) => error is FoundationApiException api
        ? $"HTTP {(int)api.Status}; code: {api.Code}; request: {api.RequestId ?? "unavailable"}.\nServer: {api.ServerMessage}"
        : error is null ? "No additional details available." : $"Connection error: {error.GetType().Name}.";
}
