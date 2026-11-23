using AmorRP.Contracts.Seasons;
using AmorRP.Core.Items;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private readonly BackgroundRefresh<Member> characterDescriptionRefresh = new(TimeSpan.FromMinutes(1));
    private Member? characterDescriptionProfile;
    private string memberDescription = "", savedMemberDescription = "";
    private string? descriptionEtag;

    private void ResetMemberDescription(Member member)
    {
        memberDescription = savedMemberDescription = member.Description;
        descriptionEtag = member.Etag;
    }

    private void ClearCharacterDescription()
    {
        characterDescriptionRefresh.Reset();
        characterDescriptionProfile = null;
        memberDescription = savedMemberDescription = "";
        descriptionEtag = null;
    }

    private void DrawCharacterDescription()
    {
        DrawSectionHeading("Character description");
        if (session is not { } actor || community is not { } season || origin is not { } uri) return;

        var now = DateTimeOffset.UtcNow;
        if (characterDescriptionRefresh.TryCollect(now, dataGeneration, out var loaded))
            characterDescriptionProfile = loaded;
        if (!SessionRenewalInProgress && !WriteBlocked)
            characterDescriptionRefresh.Start(now, dataGeneration, requests.Token,
                ct => client.GetAsync<Member>(uri, $"api/v2/seasons/{season.Id}/members/{actor.Character.Id}", actor.AccessToken, ct));

        DrawRefreshState(characterDescriptionRefresh, "Character description");
        if (characterDescriptionProfile is not { } member) return;
        if (SeasonReadOnly)
        {
            if (member.Description.Length > 0) Wrapped(member.Description);
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
            Mutate("PATCH", $"api/v2/seasons/{community!.Id}/members/{member.Character.Id}/description", new MemberDescriptionEdit(memberDescription), descriptionEtag ?? member.Etag);
        ImGui.EndDisabled();
        if (memberDescription != member.Description)
        {
            ImGui.SameLine();
            if (ImGui.Button(conflict ? "Reload description" : "Cancel edit")) ResetMemberDescription(member);
        }
    }

    private void AcceptMemberDescriptionSave(CommandRequest request, Member member)
    {
        if (request.Operation.Kind != CommandKind.EditMemberDescription || session?.Character.Id != member.Character.Id) return;
        characterDescriptionRefresh.Invalidate();
        characterDescriptionProfile = member;
        ResetMemberDescription(member);
        actionResult = "Description saved.";
    }
}
