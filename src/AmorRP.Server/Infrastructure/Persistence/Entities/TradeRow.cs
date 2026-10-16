namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class TradeRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid GroupId { get; set; }
    public Guid InitiatorId { get; set; }
    public Guid RecipientId { get; set; }
    public string State { get; set; } = "invited";
    public int Revision { get; set; } = 1;
    public long InitiatorCurrency { get; set; }
    public long RecipientCurrency { get; set; }
    public bool InitiatorConfirmed { get; set; }
    public bool RecipientConfirmed { get; set; }
    public bool InitiatorFinalConfirmed { get; set; }
    public bool RecipientFinalConfirmed { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string? EndReason { get; set; }
    public Guid? CompletionOperationId { get; set; }
}
