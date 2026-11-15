using System.Net;
using AmorRP.Contracts.Inventory;
using AmorRP.Core.Items;
using AmorRP.Plugin.Services.Api;

namespace AmorRP.Plugin.Features.Writing;

internal sealed record WritingLogSnapshot(HoldingPage Page, HoldingDetails? Selected);

internal static class WritingLogReader
{
    public static async Task<WritingLogSnapshot> ReadAsync(FoundationClient client, Uri origin, string token,
        Guid communityId, Guid characterId, string query, Guid? selected, int requestedCount, CancellationToken ct)
    {
        var prefix = $"api/v2/seasons/{communityId}/inventory";
        var page = await client.GetAsync<HoldingPage>(origin, prefix + "?" + query, token, ct);
        var items = page.Items.ToList();
        while (items.Count < requestedCount && page.NextCursor != null)
        {
            page = await client.GetAsync<HoldingPage>(origin, prefix + "?" + query + "&cursor=" + Uri.EscapeDataString(page.NextCursor), token, ct);
            items.AddRange(page.Items);
        }
        foreach (var item in items) Validate(item, communityId, characterId);
        HoldingDetails? details = null;
        if (selected.HasValue)
        {
            try { details = await client.GetAsync<HoldingDetails>(origin, prefix + "/" + selected, token, ct); }
            catch (FoundationApiException error) when (error.Status == HttpStatusCode.NotFound) { }
            if (details != null)
            {
                Validate(details.Holding, communityId, characterId);
                if (details.Holding.Id != selected || details.Letter == null || details.Letter.Id != details.Holding.LetterId
                    || details.Letter.CommunityId != communityId || details.Letter.TypeId != details.Holding.TypeId)
                    throw new InvalidOperationException("The written item response does not match this reading view.");
            }
        }
        return new(page with { Items = items.DistinctBy(x => x.Id).ToArray() }, details);
    }

    private static void Validate(Holding item, Guid communityId, Guid characterId)
    {
        if (item.CommunityId != communityId || item.CharacterId != characterId || item.Quantity < 1
            || !CraftingTypes.Writing.Contains(item.TypeId))
            throw new InvalidOperationException("The writing response does not belong to this character and community.");
    }
}
