namespace AmorRP.Plugin.Features.Communities;

internal sealed class GroupPermissionDraft
{
    private string scope = "";
    private HashSet<string> baseline = [];
    public HashSet<string> Actions { get; private set; } = [];
    public string? ETag { get; private set; }
    public bool IsDirty => !Actions.SetEquals(baseline);

    public void Refresh(string key, string etag, IEnumerable<string> actions)
    {
        if (scope != key || !IsDirty) Load(key, etag, actions);
    }
    public void Load(string key, string etag, IEnumerable<string> actions)
    { scope = key; ETag = etag; baseline = actions.ToHashSet(); Actions = baseline.ToHashSet(); }
    public void Set(string action, bool enabled) { if (enabled) Actions.Add(action); else Actions.Remove(action); }
    public void Clear() { scope = ""; ETag = null; baseline = []; Actions = []; }
}
