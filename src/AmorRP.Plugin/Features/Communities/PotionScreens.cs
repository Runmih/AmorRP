using AmorRP.Contracts.Inventory;
using AmorRP.Plugin.Services.Game;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private int recipeChoice, productionQuantity = 1, destination;
    private ConsumptionResult? lastUse;
    private bool resendConfirmed;
    private DateTimeOffset lastPost;
    private void DrawConsumables()
    {
        if (community == null) { Wrapped("Use Join or Create community above to get started."); return; }
        var shown = compact;
        foreach (var item in shown)
        {
            ImGui.PushID(item.Id.ToString());
            if (ImGui.SmallButton(pinned.Contains(item.Id) ? "Unpin" : "Pin")) TogglePin(item.Id);
            ImGui.SameLine(); if (ImGui.Selectable($"{item.Name} ×{item.Available} (revision {item.DefinitionRevision})", holdingSelection == item.Id)) ReadHolding(item);
            ImGui.PopID();
        }
        if (shown.Length == 0) Wrapped("You have no potions. Create them in Crafting or receive them in a trade.");
        LoadMoreInventory(true);
        if (details?.Definition != null) { Wrapped(details.Definition.Description); DrawPotionUse(details); }
        DrawResend();
    }
    private void DrawPotionCreation()
    {
        if (!(Owner || community!.MyCapabilities.Contains("items.potion.create")))
        {
            Wrapped("The community owner must authorize potion creation for this character. Writing books, letters and notes does not need this permission.");
            DrawPotionAccess(); return;
        }
        var recipes = definitions.Where(x => !x.Retired && x.Enabled).ToArray();
        if (recipes.Length == 0)
        {
            Wrapped(Owner ? "No enabled recipes. Open Crafting → Potion recipes to define one, then return here to create copies." : "No enabled recipes yet. Ask the owner to define and enable a potion recipe.");
            return;
        }
        recipeChoice = Math.Clamp(recipeChoice, 0, recipes.Length - 1);
        var narrow = ImGui.GetContentRegionAvail().X < ImGui.GetFontSize() * 38;
        if (!ImGui.BeginTable("ProductionLayout", narrow ? 1 : 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV)) return;
        ImGui.TableSetupColumn("Recipes", ImGuiTableColumnFlags.WidthStretch, .4f); if (!narrow) ImGui.TableSetupColumn("Create", ImGuiTableColumnFlags.WidthStretch, .6f);
        ImGui.TableNextColumn(); ImGui.BeginChild("ProductionRecipes", new(0, narrow ? ImGui.GetTextLineHeightWithSpacing() * 7 : 0), true);
        ImGui.TextUnformatted("Choose a potion");
        for (var i = 0; i < recipes.Length; i++)
        {
            var row = recipes[i]; ImGui.PushID(row.Id.ToString());
            if (ImGui.Selectable(row.Name + "###ProductionRecipe", recipeChoice == i)) recipeChoice = i;
            ImGui.TextDisabled($"{row.Potion.CreationCost} points each · revision {row.Revision}"); ImGui.PopID();
        }
        ImGui.EndChild(); ImGui.TableNextColumn(); ImGui.BeginChild("ProductionDetails", new(0, 0), true); ImGui.PushItemWidth(-1);
        var recipe = recipes[recipeChoice]; ImGui.TextUnformatted(recipe.Name); Wrapped(recipe.Description);
        ImGui.Separator(); ImGui.TextUnformatted("Message posted when used");
        ImGui.BeginChild("ProductionMessage", new(0, ImGui.GetTextLineHeightWithSpacing() * 4), true); Wrapped(recipe.Potion.UseMessage); ImGui.EndChild();
        FieldLabel("Copies to create"); ImGui.InputInt("##ProductionQuantity", ref productionQuantity);
        var cost = (long)productionQuantity * recipe.Potion.CreationCost;
        var remaining = quotas?.Items.FirstOrDefault(x => x.Kind == "potion_points")?.Remaining ?? 0;
        var max = serviceLimits?.MaxCreationQuantity ?? 100;
        Wrapped($"Cost: {recipe.Potion.CreationCost} points per potion");
        Wrapped($"You can craft {remaining / Math.Max(1, recipe.Potion.CreationCost)} more with your remaining points.");
        var error = productionQuantity < 1 || productionQuantity > max ? $"Choose between 1 and {max} copies."
            : cost > remaining ? $"Not enough points: {cost} needed, {remaining} remaining." : null;
        FieldNote("ProductionQuantity", error, $"Total: {cost} points · remaining after creation: {remaining - cost}");
        ImGui.BeginDisabled(error != null);
        if (ActionButton("Create potion copies##producePotionCopies")) Mutate("POST", $"api/v2/communities/{community!.Id}/inventory/potions", new PotionCreate(recipe.Id, recipe.Revision, productionQuantity));
        ImGui.EndDisabled();
        if (ImGui.CollapsingHeader("Check potion access")) DrawPotionAccess();
        ImGui.PopItemWidth(); ImGui.EndChild(); ImGui.EndTable();
    }
    private static string DestinationId(int index) => index switch { 0 => "emote", 1 => "say", 2 => "party", >= 3 and <= 10 => "linkshell:" + (index - 2), _ => "crossworld-linkshell:" + (index - 10) };
    private static int DestinationIndex(string id) => Enumerable.Range(0, ChatCommand.Destinations.Length).FirstOrDefault(i => DestinationId(i) == id, -1);
    private bool CanPost(string message, int channel) => ChatDestination.Available(player, party, channel)
        && ChatCommand.TryCreate(message, channel, out _, ChatLimit) && DateTimeOffset.UtcNow - lastPost >= TimeSpan.FromSeconds(3);
    private void DrawPotionUse(HoldingDetails item)
    {
        if (item.Definition == null) return;
        ImGui.Combo("Potion destination", ref destination, ChatCommand.Destinations, ChatCommand.Destinations.Length);
        var message = item.Definition.Potion.UseMessage;
        Wrapped($"Uses one {item.Holding.Name}; community {community!.Name}; character {session!.Character.DisplayName}.");
        Wrapped(ChatCommand.TryCreate(message, destination, out var preview, ChatLimit) ? "Exact message: " + preview : "This message cannot be posted safely.");
        if (destination == 2 && party.Length == 0) Wrapped("Join a party before using the party channel.");
        if (!ChatDestination.Available(player, party, destination)) Wrapped("The selected channel is unavailable. Choose a joined linkshell or available channel before consuming.");
        var error = item.Holding.Available < 1 ? "No unreserved copies are available."
            : !ChatDestination.Available(player, party, destination) ? "Choose an available channel."
            : AmorRP.Core.Items.LiteralChatText.Error(message, ChatLimit)
            ?? (DateTimeOffset.UtcNow - lastPost < TimeSpan.FromSeconds(3) ? "Wait a moment before posting another potion message." : null);
        FieldNote("PotionUse", error, "One copy is consumed when the server confirms use.");
        ImGui.BeginDisabled(item.Holding.Available < 1 || !CanPost(message, destination));
        if (ActionButton("Use one potion and post message")) { Mutate("POST", $"api/v2/communities/{community.Id}/inventory/{item.Holding.Id}/use", new ConsumptionRequest(item.Holding.Version, true, DestinationId(destination))); }
        ImGui.EndDisabled();
    }
    private void DrawResend()
    {
        if (lastUse == null) return;
        Wrapped("Last consumed message: " + lastUse.Message + " → " + lastUse.Destination);
        ImGui.Checkbox("Resend may duplicate a message already delivered", ref resendConfirmed);
        ImGui.BeginDisabled(!resendConfirmed || !CanPost(lastUse.Message, DestinationIndex(lastUse.Destination)));
        if (ActionButton("Manually resend last consumed message"))
        {
            lastPost = DateTimeOffset.UtcNow; resendConfirmed = false;
            actionResult = GameChatSender.Send(lastUse.Message, DestinationIndex(lastUse.Destination), ChatLimit) ? "Message submitted again; check the chosen channel." : "Message could not be submitted. The item remains consumed.";
        }
        ImGui.EndDisabled();
    }
}
