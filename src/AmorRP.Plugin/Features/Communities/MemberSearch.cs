using AmorRP.Contracts.Communities;
namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private void ClearMemberSearch()
    { memberSearch = appliedMemberSearch = ""; memberSearchResults = []; memberSearchCursor = null; }
    private void SearchMembers(bool more = false)
    {
        if (community == null || session == null) return;
        var query = more ? appliedMemberSearch : memberSearch.Trim();
        if (!more && query.Length == 0) { ClearMemberSearch(); return; }
        var cursor = more ? (query.Length > 0 ? memberSearchCursor : membersCursor) : null;
        var path = $"api/v2/communities/{community.Id}/members?search={Uri.EscapeDataString(query)}" + (Owner && showInactive ? "&includeDormant=true" : "")
            + (cursor != null ? "&cursor=" + Uri.EscapeDataString(cursor) : "");
        var id = community.Id; var token = session.AccessToken;
        Start(async ct => {
            var page = await client.GetAsync<MemberPage>(origin!, path, token, ct);
            return () => {
                if (community?.Id != id || (more && query != appliedMemberSearch)) return;
                if (query.Length == 0) { members = members.Concat(page.Items).DistinctBy(x => x.Character.Id).ToArray(); membersCursor = page.NextCursor; }
                else {
                    appliedMemberSearch = query;
                    memberSearchResults = (more ? memberSearchResults.Concat(page.Items) : page.Items).DistinctBy(x => x.Character.Id).ToArray();
                    memberSearchCursor = page.NextCursor;
                }
            };
        }, "Searching community members…");
    }
}
