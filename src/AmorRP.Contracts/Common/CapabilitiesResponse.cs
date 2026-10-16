namespace AmorRP.Contracts.Common;

public sealed record CapabilitiesResponse(
    string ApiVersion,
    string ServerVersion,
    string MinimumClientVersion,
    bool Maintenance,
    string Message,
    ItemTypeInfo[] SupportedTypes,
    string[] GrantableCapabilities,
    string[] ChatDestinationKinds,
    PublicLimits Limits);

public sealed record ItemTypeInfo(string Id, int TypeDataVersion, bool CanUse, bool Stackable);

public sealed record PublicLimits(
    int OwnedGroups, int JoinedGroups, int DefaultLettersPerWeek, int DefaultPotionPoints,
    int ChatMessageTextElements, int LetterTitleTextElements, int LetterBodyTextElements,
    int MaxPageSize, int MaxHoldingsPerCharacterGroup, int MaxTradeLinesPerSide,
    int MaxCreationQuantity, int TradeLifetimeSeconds, string ResetWeekdayUtc,
    string ResetTimeUtc, int MaxRequestBytes, int MaxWeeklyPotionPoints = 1000000, int MaxWeeklyLetters = 10000,
    int MaxActiveInvitationsPerGroup = 100, int OwnershipProposalHours = 24, int MaxCurrencyIconBytes = 262144,
    int MaxCurrencyIconDimension = 128, int MaxCategoriesPerGroup = 100, int MaxDefinitionsPerGroup = 1000,
    int DefinitionNameTextElements = 80, int DefinitionDescriptionTextElements = 300,
    int MaxGroupLogoBytes = 2097152, int MaxGroupLogoSourceDimension = 2048, int MaxGroupLogoDimension = 512);
