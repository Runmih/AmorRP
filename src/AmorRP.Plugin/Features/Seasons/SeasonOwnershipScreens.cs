using AmorRP.Contracts.Seasons;
using AmorRP.Contracts.Currency;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private void DrawOwnershipOffer()
    {
        if (ownershipRefresh.Error != null) DrawRefreshState(ownershipRefresh, "Ownership offer");
        if (community == null || transfer?.ToCharacterId != session!.Character.Id || transfer.Status != "proposed") return;
        Wrapped($"Ownership offered to you. Expires {transfer.ExpiresAt.LocalDateTime:g}.");
        if (ActionButton("Accept ownership")) Mutate("POST", $"api/v2/seasons/{community.Id}/ownership-transfers/{transfer.Id}/accept", null);
        ImGui.SameLine(); if (ActionButton("Decline ownership")) Mutate("POST", $"api/v2/seasons/{community.Id}/ownership-transfers/{transfer.Id}/cancel", null);
    }
    private static string ETag(string kind, Guid id, int version) => $"\"{kind}:{id:N}:{version}\"";
}
