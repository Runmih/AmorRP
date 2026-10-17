using AmorRP.Contracts.Groups;
using GroupProfile = AmorRP.Contracts.Groups.Group;

namespace AmorRP.Plugin.Features.Communities;

internal sealed class GroupProfileDraft
{
    private GroupProfile? baseline;

    public string Name = "";
    public string Description = "";
    public int JoinMode;
    public string Password = "";
    public string? ETag { get; private set; }

    public string JoinModeId => JoinMode switch
    {
        0 => "open",
        1 => "closed",
        2 => "password",
        _ => throw new InvalidOperationException("Unknown group joining mode.")
    };

    public bool IsDirty => Name != (baseline?.Name ?? "")
        || Description != (baseline?.Description ?? "")
        || JoinModeId != (baseline?.JoinMode ?? "open")
        || Password.Length > 0;

    public void Refresh(GroupProfile current)
    {
        // Keep both unfinished values and their original save precondition.
        // A remote edit must remain a visible conflict until explicitly reloaded.
        if (baseline?.Id != current.Id || !IsDirty)
            Load(current);
    }

    public void Load(GroupProfile current)
    {
        baseline = current;
        Name = current.Name;
        Description = current.Description;
        JoinMode = current.JoinMode switch
        {
            "open" => 0,
            "closed" => 1,
            "password" => 2,
            _ => throw new InvalidOperationException("Unknown group joining mode.")
        };
        Password = "";
        ETag = current.Etag;
    }

    public void Clear()
    {
        baseline = null;
        Name = Description = Password = "";
        JoinMode = 0;
        ETag = null;
    }

    public void AcceptLogoVersion(string etag) => ETag = etag;

    public GroupWrite Request() => new(Name, Description, JoinModeId, Password.Length > 0 ? Password : null);
}
