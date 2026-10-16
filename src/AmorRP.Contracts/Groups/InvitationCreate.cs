using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Groups;

// Legacy fields remain accepted for older clients; all new invitations are permanent.
public sealed record InvitationCreate(int ExpiresInHours = 0, int MaxUses = 0);
