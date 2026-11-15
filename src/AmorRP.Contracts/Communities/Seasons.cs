namespace AmorRP.Contracts.Communities;

public sealed record SeasonSummary(Guid Id, string Name, string State, int Version);
public sealed record SeasonContext(bool Enabled, bool IsAdministrator, Guid? ActiveSeasonId, SeasonSummary[] Seasons);
public sealed record SeasonTransition(string ConfirmName);
