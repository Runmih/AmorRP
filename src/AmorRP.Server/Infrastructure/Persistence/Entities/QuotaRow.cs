namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class QuotaRow
{
    public Guid GroupId { get; set; }
    public Guid CharacterId { get; set; }
    public string Kind { get; set; } = "";
    public DateTimeOffset PeriodStart { get; set; }
    public int Limit { get; set; }
    public int Used { get; set; }
}
