namespace AmorRP.Contracts.Common;
public sealed record CommandResult<T>(Guid OperationId, T Result);
public sealed record Receipt(Guid OperationId);
