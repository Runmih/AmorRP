using AmorRP.Contracts.Groups;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private bool initialAllowancesEdited;
    public Action? OpenJoinGroup { get; set; }
    public Action? OpenCreateGroup { get; set; }
    public Action? CloseJoinGroup { get; set; }
    public Action? CloseCreateGroup { get; set; }
    public bool CanDrawGroupForms => session != null && session.AccessExpiresAt > DateTimeOffset.UtcNow
        && player.IsLoaded && player.HomeWorld.IsValid
        && session.Character.DisplayName == player.CharacterName
        && session.Character.HomeWorldName == player.HomeWorld.Value.Name.ToString();

    public void DrawJoinGroup() => DrawGroupForm(create: false);
    public void DrawCreateGroup() => DrawGroupForm(create: true);

    private void DrawGroupForm(bool create)
    {
        if (!CanDrawGroupForms) return;
        var pending = configuration.PendingCommands.FirstOrDefault(x => x.Origin == origin?.AbsoluteUri && x.CharacterId == session!.Character.Id);
        var feedbackHeight = ActionFeedbackHeight(pending);
        ImGui.BeginChild("GroupFormContent", new(0, feedbackHeight > 0 ? -feedbackHeight - ImGui.GetStyle().ItemSpacing.Y : 0), false);
        ImGui.PushItemWidth(-1);
        ImGui.TextUnformatted($"{session!.Character.DisplayName} @ {session.Character.HomeWorldName}");
        ImGui.TextUnformatted($"Joined groups: {groups.Length}");
        ImGui.Spacing();
        if (create) DrawCreateGroupForm(); else DrawJoinGroupForm();
        ImGui.PopItemWidth();
        ImGui.EndChild();
        DrawActionFeedback(pending);
    }

    private void DrawJoinGroupForm()
    {
        FieldLabel("Invitation code"); ImGui.InputText("##InvitationCode", ref invitationCode, 100);
        Wrapped("Joining shares your verified character name, home world and membership with the group. Owners see management summaries and administrative history.");
        ImGui.Checkbox("I accept group member visibility", ref visibilityAccepted);
        ImGui.BeginDisabled(!visibilityAccepted || string.IsNullOrWhiteSpace(invitationCode));
        if (ActionButton("Join group")) Mutate("POST", "api/v1/groups/join", new JoinGroup(invitationCode.Trim(), visibilityAccepted));
        ImGui.EndDisabled();
    }

    private void DrawCreateGroupForm()
    {
        FieldLabel("Group name"); LimitedInput("##NewGroupName", ref name, 80, countCodeUnits: true);
        FieldLabel("Description"); WrappedInput("##NewGroupDescription", ref description, 8192, new(-1, 85), maximumCharacters: GroupEdit.MaximumDescriptionCharacters);
        FieldLabel("Currency name"); LimitedInput("##NewCurrencyName", ref currencyName, 80, countCodeUnits: true);
        Wrapped("A default coin icon is used until you upload a custom icon in the group profile.");
        FieldLabel("Initial potion points / character / week"); initialAllowancesEdited |= ImGui.InputInt("##InitialPotionPoints", ref initialPoints);
        FieldLabel("Initial written items / character / week"); initialAllowancesEdited |= ImGui.InputInt("##InitialWrittenItems", ref initialLetters);
        Wrapped("These limits establish your first week's policy. Later policy edits start next Monday.");
        if (initialPoints == 0) Wrapped("Zero potion points means no potion production in the first week.");
        if (ActionButton("Create group")) Mutate("POST", "api/v1/groups", new GroupCreate(name, description, currencyName, symbol, initialPoints, initialLetters));
    }

    private void ResetGroupForms()
    {
        invitationCode = ""; visibilityAccepted = false;
        name = description = ""; currencyName = "Coins"; symbol = "C";
        initialPoints = serviceLimits?.DefaultPotionPoints ?? 0;
        initialLetters = serviceLimits?.DefaultLettersPerWeek ?? 5;
        initialAllowancesEdited = false;
        CloseJoinGroup?.Invoke(); CloseCreateGroup?.Invoke();
    }
}
