using AmorRP.Contracts.Communities;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private bool initialAllowancesEdited;
    public Action? OpenJoinCommunity { get; set; }
    public Action? OpenCreateCommunity { get; set; }
    public Action? CloseJoinCommunity { get; set; }
    public Action? CloseCreateCommunity { get; set; }
    public bool CanDrawCommunityForms => session != null && session.AccessExpiresAt > DateTimeOffset.UtcNow
        && player.IsLoaded && player.HomeWorld.IsValid
        && session.Character.DisplayName == player.CharacterName
        && session.Character.HomeWorldName == player.HomeWorld.Value.Name.ToString();

    public void DrawJoinCommunity() => DrawCommunityForm(create: false);
    public void DrawCreateCommunity() => DrawCommunityForm(create: true);

    private void DrawCommunityForm(bool create)
    {
        if (!CanDrawCommunityForms) return;
        var pending = configuration.PendingCommands.FirstOrDefault(x => x.Origin == origin?.AbsoluteUri && x.CharacterId == session!.Character.Id);
        var feedbackHeight = ActionFeedbackHeight(pending);
        ImGui.BeginChild("CommunityFormContent", new(0, feedbackHeight > 0 ? -feedbackHeight - ImGui.GetStyle().ItemSpacing.Y : 0), false);
        ImGui.PushItemWidth(-1);
        ImGui.TextUnformatted($"{session!.Character.DisplayName} @ {session.Character.HomeWorldName}");
        ImGui.TextUnformatted($"Joined communities: {communities.Length}");
        ImGui.Spacing();
        if (create) DrawCreateCommunityForm(); else DrawJoinCommunityForm();
        ImGui.PopItemWidth();
        ImGui.EndChild();
        DrawActionFeedback(pending);
    }

    private void DrawJoinCommunityForm()
    {
        FieldLabel("Invitation code"); ImGui.InputText("##InvitationCode", ref invitationCode, 100);
        Wrapped("Joining shares your verified character name, home world and membership with the community. Owners see management summaries and administrative history.");
        ImGui.Checkbox("I accept community member visibility", ref visibilityAccepted);
        ImGui.BeginDisabled(!visibilityAccepted || string.IsNullOrWhiteSpace(invitationCode));
        if (ActionButton("Join community")) Mutate("POST", "api/v2/communities/join", new JoinCommunity(invitationCode.Trim(), visibilityAccepted));
        ImGui.EndDisabled();
    }

    private void DrawCreateCommunityForm()
    {
        FieldLabel("Community name"); LimitedInput("##NewCommunityName", ref name, 80, countCodeUnits: true);
        FieldLabel("Description"); WrappedInput("##NewCommunityDescription", ref description, 8192, new(-1, 85), maximumCharacters: CommunityEdit.MaximumDescriptionCharacters);
        FieldLabel("Currency name"); LimitedInput("##NewCurrencyName", ref currencyName, 80, countCodeUnits: true);
        Wrapped("A default coin icon is used until you upload a custom icon in the community profile.");
        FieldLabel("Initial potion points / character / week"); initialAllowancesEdited |= ImGui.InputInt("##InitialPotionPoints", ref initialPoints);
        FieldLabel("Initial written items / character / week"); initialAllowancesEdited |= ImGui.InputInt("##InitialWrittenItems", ref initialLetters);
        Wrapped("These limits establish your first week's policy. Later policy edits start next Monday.");
        if (initialPoints == 0) Wrapped("Zero potion points means no potion production in the first week.");
        if (ActionButton("Create community")) Mutate("POST", "api/v2/communities", new CommunityCreate(name, description, currencyName, symbol, initialPoints, initialLetters));
    }

    private void ResetCommunityForms()
    {
        invitationCode = ""; visibilityAccepted = false;
        name = description = ""; currencyName = "Coins"; symbol = "C";
        initialPoints = serviceLimits?.DefaultPotionPoints ?? 0;
        initialLetters = serviceLimits?.DefaultLettersPerWeek ?? 5;
        initialAllowancesEdited = false;
        CloseJoinCommunity?.Invoke(); CloseCreateCommunity?.Invoke();
    }
}
