namespace AmorRP.Contracts.Groups;

public sealed record Group(Guid Id, Guid CommunityId, string Name, string Description, Guid? LogoAssetId, int LogoVersion, int Version, string Etag);
public sealed record GroupWrite(string Name, string Description);
public sealed record GroupPage(Group[] Items, string? NextCursor);
