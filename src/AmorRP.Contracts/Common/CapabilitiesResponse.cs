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
    int OwnedCommunities, int JoinedCommunities, int DefaultLettersPerWeek, int DefaultPotionPoints,
    int ChatMessageTextElements, int LetterTitleTextElements, int LetterBodyTextElements,
    int MaxPageSize, int MaxHoldingsPerCharacterCommunity, int MaxTradeLinesPerSide,
    int MaxCreationQuantity, int TradeLifetimeSeconds, string ResetWeekdayUtc,
    string ResetTimeUtc, int MaxRequestBytes, int MaxWeeklyPotionPoints = 1000000, int MaxWeeklyLetters = 10000,
    int MaxActiveInvitationsPerCommunity = 100, int OwnershipProposalHours = 24, int MaxCurrencyIconBytes = 262144,
    int MaxCurrencyIconDimension = 128, int MaxCategoriesPerCommunity = 100, int MaxDefinitionsPerCommunity = 1000,
    int DefinitionNameTextElements = 80, int DefinitionDescriptionTextElements = 300,
    int MaxCommunityLogoBytes = 2097152, int MaxCommunityLogoSourceDimension = 2048, int MaxCommunityLogoDimension = 512);
