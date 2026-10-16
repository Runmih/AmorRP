namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class UsedRefreshRow
{
    public string TokenHash { get; set; } = "";
    public Guid SessionId { get; set; }
    public string NextRefreshHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
}
