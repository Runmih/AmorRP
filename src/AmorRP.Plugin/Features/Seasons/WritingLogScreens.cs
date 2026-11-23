using System.Net;
using AmorRP.Contracts.Inventory;
using AmorRP.Plugin.Features.Inventory;
using AmorRP.Plugin.Features.Shell;
using AmorRP.Plugin.Features.Writing;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Identity;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private readonly InventoryCompanionQuery writingLogQuery = new() { Section = InventoryCompanionSection.Writing };
    private readonly BackgroundRefresh<WritingLogSnapshot> writingLogRefresh = new(TimeSpan.FromMinutes(1));
    private HoldingPage writingLogPage = new([], null, "0");
    private HoldingDetails? writingLogDetails;
    private Guid? writingLogSelection;
    private string writingLogSearch = "";
    private int writingLogRequestedCount;
    private bool writingLogVisible, writingLogComposing;
    private void OpenWritingLog(Holding item)
    {
        if (community?.Id == item.CommunityId && session?.Character.Id == item.CharacterId
            && AmorRP.Core.Items.CraftingTypes.Writing.Contains(item.TypeId))
        {
            writingLogSearch = writingLogQuery.Type = "";
            ResetWritingLogFilters();
            writingLogSelection = item.Id; writingLogComposing = false;
        }
        OpenWorkspace(WorkspaceKind.Writing);
    }

    public void SetWritingLogVisible(bool visible)
    {
        if (visible && !writingLogVisible) { writingLogRefresh.Invalidate(); refreshNeeded = true; }
        writingLogVisible = visible;
    }

    private void ResetWritingLog()
    {
        writingLogQuery.Reset(); writingLogQuery.Section = InventoryCompanionSection.Writing;
        writingLogSearch = ""; writingLogComposing = false;
        ResetWritingLogFilters();
    }

    private void ResetWritingLogFilters()
    {
        writingLogQuery.Search = writingLogSearch;
        writingLogRefresh.Reset(); writingLogPage = new([], null, "0");
        writingLogSelection = null; writingLogDetails = null; writingLogRequestedCount = 0;
    }

    private void CollectWritingLog(DateTimeOffset now)
    {
        if (writingLogRefresh.TryCollect(now, dataGeneration, out var snapshot))
        {
            writingLogPage = snapshot.Page; writingLogDetails = snapshot.Selected;
            if (writingLogSelection.HasValue && snapshot.Selected == null)
            { writingLogSelection = null; editingLetter = null; letterEditTitle = letterEditBody = ""; }
        }
        if (writingLogRefresh.Error is FoundationApiException { Status: HttpStatusCode.Forbidden or HttpStatusCode.NotFound })
        {
            writingLogPage = new([], null, "0"); writingLogDetails = null; writingLogSelection = null;
            editingLetter = null; letterEditTitle = letterEditBody = ""; writingLogComposing = false;
        }
    }

    private void StartWritingLog(DateTimeOffset now, Uri uri, string token, Guid communityId, Guid characterId, CancellationToken ct)
    {
        if (!writingLogVisible || Busy) return;
        var query = writingLogQuery.Build(); var selection = writingLogSelection;
        var count = Math.Max(writingLogRequestedCount, writingLogPage.Items.Length);
        writingLogRefresh.Start(now, dataGeneration, ct,
            c => WritingLogReader.ReadAsync(client, uri, token, communityId, characterId, query, selection, count, c));
    }

    private void DrawWritingLog()
    {
        ImGui.TextColored(InventoryGold, "WRITING LOG");
        DrawSeasonState();
        if (community == null)
        {
            DrawNoSeason();
            return;
        }
        ImGui.BeginDisabled(SeasonReadOnly);
        if (ImGui.Button("New writing")) { writingLogComposing = true; editingLetter = null; }
        ImGui.EndDisabled();
        if (writingLogComposing)
        { ImGui.SameLine(); if (ImGui.Button("Back to reading")) writingLogComposing = false; }
        ImGui.SameLine();
        ImGui.BeginChild("WritingLogContent", System.Numerics.Vector2.Zero, false);
        var stacked = ImGui.GetContentRegionAvail().X < ImGui.GetFontSize() * 46;
        if (ImGui.BeginTable("WritingLogLayout", stacked ? 1 : 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV))
        {
            ImGui.TableSetupColumn("Library", ImGuiTableColumnFlags.WidthStretch, .32f);
            if (!stacked) ImGui.TableSetupColumn("Reading", ImGuiTableColumnFlags.WidthStretch, .68f);
            ImGui.TableNextColumn();
            ImGui.BeginChild("WritingLibrary", new(0, stacked ? ImGui.GetTextLineHeightWithSpacing() * 10 : 0), true);
            DrawWritingLibrary(); ImGui.EndChild();
            ImGui.TableNextColumn(); ImGui.BeginChild("WritingReader", new(0, 0), true); ImGui.PushItemWidth(-1);
            if (writingLogComposing)
            {
                DrawRefreshState(inventoryRefresh, "Writing allowance");
                var editorHeight = Math.Max(ImGui.GetTextLineHeightWithSpacing() * 6,
                    ImGui.GetContentRegionAvail().Y - ImGui.GetFrameHeightWithSpacing() * 15);
                if (!inventoryRefresh.HasLoaded && categories.Length == 0) Wrapped("Loading your writing allowance and categories…");
                else DrawLetterCreation(editorHeight);
            }
            else DrawWritingReader();
            ImGui.PopItemWidth(); ImGui.EndChild(); ImGui.EndTable();
        }
        ImGui.EndChild();
    }

    private void DrawWritingLibrary()
    {
        ImGui.TextDisabled("YOUR BOOKS, LETTERS & NOTES");
        ImGui.SetNextItemWidth(-1);
        var entered = ImGui.InputTextWithHint("##WritingSearch", "Search titles…", ref writingLogSearch, 100, ImGuiInputTextFlags.EnterReturnsTrue);
        if (entered || ImGui.IsItemDeactivatedAfterEdit()) ResetWritingLogFilters();
        var ids = new[] { "", "book", "letter", "note" };
        var type = Math.Max(0, Array.IndexOf(ids, writingLogQuery.Type));
        ImGui.SetNextItemWidth(-1);
        if (ImGui.Combo("##WritingTypes", ref type, new[] { "All writing", "Books", "Letters", "Notes" }, 4))
        { writingLogQuery.Type = ids[type]; ResetWritingLogFilters(); }
        var sortOrder = writingLogQuery.Sort;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.Combo("##WritingSort", ref sortOrder, new[] { "Sort: title", "Sort: type" }, 2))
        { writingLogQuery.Sort = sortOrder; ResetWritingLogFilters(); }
        DrawRefreshState(writingLogRefresh, "Writing"); ImGui.Separator();
        if (ImGui.BeginTable("WritingEntries", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH))
        {
            ImGui.TableSetupColumn("Title", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableHeadersRow();
            foreach (var item in writingLogPage.Items)
            {
                ImGui.PushID(item.Id.ToString());
                ImGui.TableNextRow(); ImGui.TableNextColumn();
                if (ImGui.Selectable(item.Name + "###WritingEntry", writingLogSelection == item.Id, ImGuiSelectableFlags.SpanAllColumns))
                {
                    writingLogSelection = item.Id; writingLogDetails = null; writingLogComposing = false;
                    writingLogRefresh.Invalidate();
                }
                ImGui.TableNextColumn(); ImGui.TextUnformatted(TypeName(item.TypeId));
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        if (writingLogPage.Items.Length == 0 && writingLogRefresh.HasLoaded)
            Wrapped("No matching writing. Clear the search, create a text item or receive one in a trade.");
        if (writingLogPage.NextCursor != null && ImGui.Button("More writing"))
        { writingLogRequestedCount = writingLogPage.Items.Length + 1; writingLogRefresh.Invalidate(); }
    }

    private void DrawWritingReader()
    {
        if (writingLogDetails?.Letter is not { } letter)
        {
            Wrapped(writingLogSelection.HasValue ? "Opening this written item…" : "Choose a book, letter or note from the log to read it here.");
            return;
        }
        ImGui.PushStyleColor(ImGuiCol.Text, InventoryGold); Wrapped(letter.Title); ImGui.PopStyleColor();
        Wrapped($"By {letter.Author.DisplayName} @ {letter.Author.HomeWorldName}");
        ImGui.TextDisabled(TypeName(letter.TypeId) + (letter.Locked ? " · sealed after trading" : " · original writing"));
        var reserved = writingLogDetails.Holding.Reserved;
        if (ImGui.Button("Copy text")) ImGui.SetClipboardText(letter.Body);
        if (!SeasonReadOnly && letter.Author.Id == session!.Character.Id && !letter.Locked)
        {
            ImGui.SameLine(); ImGui.BeginDisabled(reserved > 0);
            if (ImGui.Button(editingLetter == letter.Id ? "Reload saved text" : "Edit writing"))
            { editingLetter = letter.Id; letterEditVersion = letter.Version; letterEditTitle = letter.Title; letterEditBody = letter.Body; }
            ImGui.EndDisabled();
        }
        ImGui.Separator();
        if (!SeasonReadOnly && editingLetter == letter.Id && letter.Author.Id == session!.Character.Id && !letter.Locked)
        { DrawWritingLogEditor(letter, reserved); return; }
        var body = letter.Body;
        WrappedInput("##WritingReadBody", ref body, Math.Max(1024, System.Text.Encoding.UTF8.GetByteCount(body) + 1),
            new(-1, Math.Max(ImGui.GetTextLineHeightWithSpacing() * 4, ImGui.GetContentRegionAvail().Y)), readOnly: true);
    }

    private void NoteWritingLogCompletion(CommandRequest request, CommandResponse response)
    {
        if (request.Operation.Kind == CommandKind.CreateWriting && request.Json != null && writingLogComposing)
        {
            var submitted = System.Text.Json.JsonSerializer.Deserialize<LetterCreate>(request.Json, FoundationClient.Json)!;
            if (letterTitle == submitted.Title && letterBody == submitted.Body)
            {
                var produced = System.Text.Json.JsonSerializer.Deserialize<AmorRP.Contracts.Common.CommandResult<ProductionResult>>(response.Json, FoundationClient.Json)!.Result;
                if (produced.Holding.CommunityId != community?.Id || produced.Holding.CharacterId != session?.Character.Id) return;
                writingLogQuery.Type = submitted.TypeId; writingLogSearch = ""; ResetWritingLogFilters();
                writingLogSelection = produced.Holding.Id; writingLogDetails = null; writingLogComposing = false;
                writingLogRefresh.Invalidate();
            }
        }
        if (request.Operation.Kind == CommandKind.EditWriting)
        {
            var written = System.Text.Json.JsonSerializer.Deserialize<AmorRP.Contracts.Common.CommandResult<Letter>>(response.Json, FoundationClient.Json)!.Result;
            if (writingLogDetails?.Letter?.Id == written.Id && written.CommunityId == community?.Id)
                writingLogDetails = writingLogDetails with { Letter = written, Holding = writingLogDetails.Holding with { Name = written.Title } };
        }
    }
}
