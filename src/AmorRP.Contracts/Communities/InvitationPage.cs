namespace AmorRP.Contracts.Communities;

public sealed record InvitationPage(Invitation[] Items, string? NextCursor, string SnapshotVersion, string? EventCursor);
