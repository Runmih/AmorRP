namespace AmorRP.Contracts.Groups;

public sealed record GroupStock(Guid Id, string Name, string TypeId, int Quantity, int Version);
public sealed record GroupStorage(GroupStock[] Items);
public sealed record GroupStockMove(Guid ItemId, int Version, int Quantity, bool Withdraw);
public sealed record GroupShift(Guid Id, Guid CharacterId, string CharacterName, DateTimeOffset Start, DateTimeOffset End);
public sealed record GroupShifts(GroupShift[] Items);
public sealed record GroupShiftCreate(Guid CharacterId, DateTimeOffset Start, DateTimeOffset End);
