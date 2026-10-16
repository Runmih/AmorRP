using System.Security.Cryptography;
using System.Text;
using AmorRP.Contracts.Groups;
using AmorRP.Core.Items;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private string personalNote = "", savedPersonalNote = "", noteKey = "";
    private bool noteUnreadable;
    private string PersonalNoteKey(Member member) => $"{origin!.AbsoluteUri}|{session!.Character.Id}|{group!.Id}|{member.Character.Id}";
    private void LoadPersonalNote(Member member)
    {
        noteKey = PersonalNoteKey(member); personalNote = savedPersonalNote = ""; noteUnreadable = false;
        if (!configuration.ProtectedMemberNotes.TryGetValue(noteKey, out var encrypted)) return;
        try {
            personalNote = savedPersonalNote = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(encrypted), Encoding.UTF8.GetBytes("AmorRP.note.v1|" + noteKey), DataProtectionScope.CurrentUser));
        } catch (Exception ex) when (ex is CryptographicException or FormatException) { noteUnreadable = true; }
    }
    private void DrawPersonalNote(Member member)
    {
        if (noteKey != PersonalNoteKey(member)) LoadPersonalNote(member);
        ImGui.Separator(); ImGui.TextUnformatted("Your private notes");
        Wrapped("Only you can read these notes. They are saved on this computer.");
        if (noteUnreadable) { Wrapped("This note could not be unlocked with your Windows account. Its saved copy has been kept."); return; }
        WrappedInput("##PersonalNote", ref personalNote, 65536, new(-1, ImGui.GetTextLineHeightWithSpacing() * 4), maximumCharacters: 300);
        ImGui.TextDisabled($"{PlainText.Characters(personalNote)}/300");
        var invalid = PlainText.Error(personalNote, 300, multiline: true, required: false);
        if (invalid != null) Wrapped(invalid);
        ImGui.BeginDisabled(invalid != null || personalNote == savedPersonalNote);
        if (ImGui.Button("Save private note"))
        {
            try {
                if (personalNote.Length == 0) configuration.ProtectedMemberNotes.Remove(noteKey);
                else configuration.ProtectedMemberNotes[noteKey] = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(personalNote), Encoding.UTF8.GetBytes("AmorRP.note.v1|" + noteKey), DataProtectionScope.CurrentUser));
                save(); savedPersonalNote = personalNote; actionResult = "Private note saved on this computer.";
            } catch (CryptographicException) { actionResult = "Your note could not be saved. Please try again."; }
        }
        ImGui.EndDisabled(); ImGui.Separator();
    }
}
