namespace AmorRP.Contracts.Groups;

public sealed record InvitationPage(Invitation[] Items, string? NextCursor, string SnapshotVersion, string? EventCursor);
