using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private void DrawOwnershipCancellation()
    {
        if (!Owner || community == null) return;
        if (transfer is { Status: "proposed" })
        {
            Wrapped("An ownership proposal is pending.");
            if (ActionButton("Cancel ownership proposal")) Mutate("POST", $"api/v2/seasons/{community.Id}/ownership-transfers/{transfer.Id}/cancel", null);
        }
    }
}
