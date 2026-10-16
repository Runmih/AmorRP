namespace AmorRP.Contracts.Auth;

public sealed record SessionInfoPage(SessionInfo[] Items, string? NextCursor, string SnapshotVersion, string? EventCursor);
