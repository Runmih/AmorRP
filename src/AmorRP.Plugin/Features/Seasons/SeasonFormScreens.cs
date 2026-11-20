using AmorRP.Contracts.Seasons;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private bool initialAllowancesEdited;
    public Action? OpenCreateCommunity { get; set; }
    public Action? CloseCreateCommunity { get; set; }
    public bool CanDrawCommunityForms => CanCreateSeasons && session != null && session.AccessExpiresAt > DateTimeOffset.UtcNow
        && player.IsLoaded && player.HomeWorld.IsValid
        && session.Character.DisplayName == player.CharacterName
        && session.Character.HomeWorldName == player.HomeWorld.Value.Name.ToString();

    public void DrawCreateCommunity() => DrawCommunityForm();

    private void DrawCommunityForm()
    {
        if (!CanDrawCommunityForms) return;
        if (!CanCreateSeasons) return;
        ImGui.BeginChild("CommunityFormContent", System.Numerics.Vector2.Zero, false);
        ImGui.PushItemWidth(-1);
        ImGui.TextUnformatted($"{session!.Character.DisplayName} @ {session.Character.HomeWorldName}");
        ImGui.TextUnformatted($"Seasons: {communities.Length}");
        ImGui.Spacing();
        DrawCreateCommunityForm();
        ImGui.PopItemWidth();
        ImGui.EndChild();

    }

    private void DrawCreateCommunityForm()
    {
        FieldLabel("Season name"); LimitedInput("##NewCommunityName", ref name, 80, countCodeUnits: true);
        FieldLabel("Description"); WrappedInput("##NewCommunityDescription", ref description, 8192, new(-1, 85), maximumCharacters: SeasonEdit.MaximumDescriptionCharacters);
        FieldLabel("Currency name"); LimitedInput("##NewCurrencyName", ref currencyName, 80, countCodeUnits: true);
        Wrapped("A default coin icon is used until you upload a custom icon in the season profile.");
        FieldLabel("Initial Alchemy points / character / week"); initialAllowancesEdited |= ImGui.InputInt("##InitialPotionPoints", ref initialPoints);
        FieldLabel("Initial Cooking points / character / week"); initialAllowancesEdited |= ImGui.InputInt("##InitialCookingPoints", ref initialCooking);
        FieldLabel("Initial written items / character / week"); initialAllowancesEdited |= ImGui.InputInt("##InitialWrittenItems", ref initialLetters);
        Wrapped("These limits establish your first week's policy. Later policy edits start next Monday.");
        if (initialPoints == 0) Wrapped("Zero Alchemy points means no Alchemy crafting in the first week.");
        Wrapped("Creates a draft with a fresh economy. Activate it from Season management when ready.");
        ImGui.BeginDisabled(WriteBlocked);
        if (ImGui.Button("Create season")) Mutate("POST", "api/v2/seasons", new SeasonCreate(name, description, currencyName, symbol, initialPoints, initialLetters, initialCooking));
        ImGui.EndDisabled();
    }

    private void ResetCommunityForms()
    {
        name = description = ""; currencyName = "Coins"; symbol = "C";
        initialCooking = 0;
        initialPoints = serviceLimits?.DefaultPotionPoints ?? 0;
        initialLetters = serviceLimits?.DefaultLettersPerWeek ?? 5;
        initialAllowancesEdited = false;
        CloseCreateCommunity?.Invoke();
    }
}
