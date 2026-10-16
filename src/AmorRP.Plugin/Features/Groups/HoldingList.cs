using AmorRP.Contracts.Inventory;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private static void DrawHoldingRows(string id, Holding[] items, Guid? selected, Action<Holding> select)
    {
        if (!ImGui.BeginTable(id, 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp)) return;
        ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch, 3);
        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableSetupColumn("Available", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Available").X);
        ImGui.TableSetupColumn("Reserved", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Reserved").X);
        ImGui.TableHeadersRow();
        foreach (var item in items)
        {
            ImGui.PushID(item.Id.ToString()); ImGui.TableNextRow(); ImGui.TableNextColumn();
            if (ImGui.Selectable(item.Name + "###Holding", selected == item.Id, ImGuiSelectableFlags.SpanAllColumns)) select(item);
            ImGui.TableNextColumn(); ImGui.TextUnformatted(TypeName(item.TypeId));
            ImGui.TableNextColumn(); ImGui.TextUnformatted(item.Available.ToString());
            ImGui.TableNextColumn(); ImGui.TextUnformatted(item.Reserved.ToString()); ImGui.PopID();
        }
        ImGui.EndTable();
    }
}
