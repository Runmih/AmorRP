using AmorRP.Contracts.Groups;
using AmorRP.Core.Items;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private string memberDescription = "", savedMemberDescription = "";
    private string? descriptionEtag;

    private void ResetMemberDescription(Member member)
    {
        memberDescription = savedMemberDescription = member.Description;
        descriptionEtag = member.Etag;
    }

    private void ClearMemberDescription()
    { memberDescription = savedMemberDescription = ""; descriptionEtag = null; }

    private void DrawMemberDescription(Member member)
    {
        if (member.Character.Id != session!.Character.Id || member.Status != "active")
        {
            Wrapped(member.Description.Length > 0 ? member.Description : "No description yet.");
            return;
        }
        // Background refreshes may update an untouched description, but must
        // preserve a draft and its concurrency token if the saved text changed.
        if (memberDescription == savedMemberDescription) ResetMemberDescription(member);
        else if (savedMemberDescription == member.Description) descriptionEtag = member.Etag;
        ImGui.BeginDisabled(WriteBlocked);
        WrappedInput("##CharacterDescription", ref memberDescription, 65536,
            new(-1, ImGui.GetTextLineHeightWithSpacing() * 7), maximumCharacters: MemberDescriptionEdit.MaximumCharacters);
        ImGui.EndDisabled();
        ImGui.TextDisabled($"{PlainText.Characters(memberDescription)}/{MemberDescriptionEdit.MaximumCharacters}");
        var error = PlainText.Error(memberDescription, MemberDescriptionEdit.MaximumCharacters, multiline: true, required: false);
        if (error != null) Wrapped(error);
        var conflict = savedMemberDescription != member.Description;
        if (conflict) Wrapped("Your description changed elsewhere. Reload it before saving.");
        ImGui.BeginDisabled(error != null || conflict || memberDescription == member.Description);
        if (ActionButton("Save description"))
            Mutate("PATCH", $"api/v1/groups/{group!.Id}/members/{member.Character.Id}/description", new MemberDescriptionEdit(memberDescription), descriptionEtag ?? member.Etag);
        ImGui.EndDisabled();
        if (memberDescription != member.Description)
        {
            ImGui.SameLine();
            if (ImGui.Button(conflict ? "Reload description" : "Cancel edit")) ResetMemberDescription(member);
        }
    }

    private void AcceptMemberDescriptionSave(CommandRequest request, Member member)
    {
        if (!request.Path.EndsWith("/description", StringComparison.Ordinal) || memberSelection != member.Character.Id) return;
        ResetMemberDescription(member); actionResult = "Description saved.";
    }
}
