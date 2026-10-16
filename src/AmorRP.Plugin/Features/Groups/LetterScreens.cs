using AmorRP.Contracts.Inventory;
using AmorRP.Core.Items;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private string letterTitle = "", letterBody = "", letterEditTitle = "", letterEditBody = "";
    private Guid? editingLetter;
    private int letterEditVersion;
    private int letterCategory, writingType;
    private void DrawLetterCreation()
    {
        ImGui.BeginChild("WritingForm", new(0, 0), true); ImGui.PushItemWidth(-1);
        var types = new[] { "book", "letter", "note" };
        FieldLabel("Written item type"); ImGui.Combo("##WritingType", ref writingType, new[] { "Book", "Letter", "Note" }, 3);
        var choices = categories.Where(x => !x.Retired && x.AllowedTypeIds.Contains(types[writingType])).ToArray();
        if (choices.Length == 0) { Wrapped("No active Books category accepts this type. Ask the group owner to check the catalog."); ImGui.PopItemWidth(); ImGui.EndChild(); return; }
        letterCategory = Math.Clamp(letterCategory, 0, choices.Length - 1);
        if (choices.Length > 1) { FieldLabel("Category"); ImGui.Combo("##WritingCategory", ref letterCategory, choices.Select(x => x.Name).ToArray(), choices.Length); }
        var titleLimit = serviceLimits?.LetterTitleTextElements ?? 80; var bodyLimit = Math.Min(500, serviceLimits?.LetterBodyTextElements ?? 500);
        FieldLabel("Title"); LimitedInput("##WritingTitle", ref letterTitle, titleLimit);
        var titleError = PlainText.Error(letterTitle, titleLimit);
        FieldNote("WritingTitle", titleError, $"{PlainText.Characters(letterTitle)}/{titleLimit} characters");
        FieldLabel("Contents"); WrappedInput("##WritingBody", ref letterBody, 524288, new(-1, ImGui.GetTextLineHeightWithSpacing() * 10), maximumCharacters: bodyLimit);
        var bodyError = PlainText.Error(letterBody, bodyLimit, multiline: true, required: false);
        FieldNote("WritingBody", bodyError, $"{PlainText.Characters(letterBody)}/{bodyLimit} characters · contents stay private to the holder");
        var remaining = quotas?.Items.FirstOrDefault(x => x.Kind == "letters")?.Remaining ?? 0;
        Wrapped("Each book, letter or note costs one allowance. Edits and reading are free.");
        if (remaining < 1) Wrapped("Your weekly writing allowance is exhausted. Its reset time is shown above.");
        ImGui.BeginDisabled(titleError != null || bodyError != null || remaining < 1);
        if (ActionButton("Create " + types[writingType])) Mutate("POST", $"api/v1/groups/{group!.Id}/inventory/letters", new LetterCreate(choices[letterCategory].Id, letterTitle, letterBody, types[writingType]));
        ImGui.EndDisabled(); ImGui.PopItemWidth(); ImGui.EndChild();
    }
    private void DrawLetterDetails(Letter letter)
    {
        Wrapped($"By {letter.Author.DisplayName} @ {letter.Author.HomeWorldName}");
        Wrapped(letter.Locked ? "Permanently sealed after its first trade." : "The author may edit this while holding it, before its first trade.");
        var body = letter.Body;
        WrappedInput("##ReadWrittenBody", ref body, Math.Max(1024, System.Text.Encoding.UTF8.GetByteCount(body) + 1), new(-1, ImGui.GetTextLineHeightWithSpacing() * 10), readOnly: true);
        if (letter.Author.Id != session!.Character.Id || letter.Locked) return;
        ImGui.BeginDisabled(details?.Holding.Reserved > 0);
        if (ImGui.Button("Edit contents")) { editingLetter = letter.Id; letterEditVersion = letter.Version; letterEditTitle = letter.Title; letterEditBody = letter.Body; }
        ImGui.EndDisabled();
        if (details?.Holding.Reserved > 0) Wrapped("This text is reserved for a trade. End the trade before editing.");
        if (editingLetter != letter.Id) return;
        var stale = letter.Version != letterEditVersion;
        if (stale) Wrapped("This text changed. Click Edit contents to reload the saved version before saving.");
        FieldLabel("Edited title"); LimitedInput("##EditedTitle", ref letterEditTitle, serviceLimits?.LetterTitleTextElements ?? 80);
        var titleError = PlainText.Error(letterEditTitle, serviceLimits?.LetterTitleTextElements ?? 80);
        FieldNote("EditTitle", titleError, "The title shown in inventories and trade summaries.");
        FieldLabel("Edited contents"); WrappedInput("##EditedBody", ref letterEditBody, 524288, new(-1, ImGui.GetTextLineHeightWithSpacing() * 8), maximumCharacters: Math.Min(500, serviceLimits?.LetterBodyTextElements ?? 500));
        var bodyError = PlainText.Error(letterEditBody, Math.Min(500, serviceLimits?.LetterBodyTextElements ?? 500), multiline: true, required: false);
        FieldNote("EditBody", bodyError, "Editing does not spend writing allowance.");
        ImGui.BeginDisabled(stale || titleError != null || bodyError != null || details?.Holding.Reserved > 0);
        if (ActionButton("Save contents")) Mutate("PATCH", $"api/v1/groups/{group!.Id}/letters/{letter.Id}", new LetterEdit(letterEditTitle, letterEditBody), ETag("letter", letter.Id, letterEditVersion));
        ImGui.EndDisabled(); ImGui.SameLine(); if (ImGui.Button("Cancel edit")) editingLetter = null;
    }
    private void ClearCreationDrafts()
    {
        letterTitle = letterBody = letterEditTitle = letterEditBody = ""; editingLetter = null; letterCategory = recipeChoice = 0; productionQuantity = 1;
        definitionEditId = null; definitionName = definitionDescription = definitionMessage = ""; definitionCost = 1; definitionEnabled = true;
    }
}
