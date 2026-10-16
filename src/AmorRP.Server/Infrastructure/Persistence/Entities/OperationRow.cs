namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class OperationRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Scope { get; set; } = "";
    public Guid Key { get; set; }
    public string RequestHash { get; set; } = "";
    public Guid? ActorCharacterId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? SubjectCharacterId { get; set; }
    public Guid? TradeId { get; set; }
    public string Kind { get; set; } = "";
    public string Summary { get; set; } = "";
    public string? Reason { get; set; }
    public long? Delta { get; set; }
    public long? BeforeBalance { get; set; }
    public long? AfterBalance { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string ProtectedResponse { get; set; } = "";
    public string? ETag { get; set; }
    public int ResponseStatus { get; set; } = 200;
}
