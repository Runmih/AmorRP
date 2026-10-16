using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Currency;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private void LeaveGroup()
    {
        if (group == null || session == null || Owner || ActiveTrade != null || WriteBlocked) return;
        var id = session.Character.Id; var target = members.FirstOrDefault(x => x.Character.Id == id);
        var path = $"api/v1/groups/{group.Id}/members/{id}";
        if (target != null) Mutate("DELETE", path, new MemberRemoval(""), target.Etag);
        else
        {
            var uri = origin!; var token = session.AccessToken;
            Start(async ct => { var own = await client.GetAsync<Member>(uri, path, token, ct); return () => Mutate("DELETE", path, new MemberRemoval(""), own.Etag); }, "Reading current membership…");
        }
    }
    private void DrawOwnershipOffer()
    {
        if (group == null || transfer?.ToCharacterId != session!.Character.Id || transfer.Status != "proposed") return;
        Wrapped($"Ownership offered to you. Expires {transfer.ExpiresAt.LocalDateTime:g}.");
        if (ActionButton("Accept ownership")) Mutate("POST", $"api/v1/groups/{group.Id}/ownership-transfers/{transfer.Id}/accept", null);
        ImGui.SameLine(); if (ActionButton("Decline ownership")) Mutate("POST", $"api/v1/groups/{group.Id}/ownership-transfers/{transfer.Id}/cancel", null);
    }
    private static string ETag(string kind, Guid id, int version) => $"\"{kind}:{id:N}:{version}\"";
}
