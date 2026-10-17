using System.ComponentModel.DataAnnotations;
using AmorRP.Contracts.Common;
using AmorRP.Core.Communities;

namespace AmorRP.Server.Options;

public sealed class ServicePolicyOptions : IValidatableObject
{
    [Range(2, 1000)] public int MaxCategoriesPerCommunity { get; set; } = 100;
    [Range(1, 10000)] public int MaxDefinitionsPerCommunity { get; set; } = 1000;
    [Range(1024, 2097152)] public int MaxCommunityLogoBytes { get; set; } = 2097152;
    [Range(1024, 1048576)] public int MaxCurrencyIconBytes { get; set; } = 262144;
    [Range(0, 10000000)] public int MaxWeeklyPotionPoints { get; set; } = 1000000;
    [Range(0, 100000)] public int MaxWeeklyLetters { get; set; } = 10000;
    [Range(1, 1000)] public int MaxActiveInvitationsPerCommunity { get; set; } = 100;
    [Range(1, 720)] public int OwnershipProposalHours { get; set; } = 24;
    // Budgets are per client IPv4 address / IPv6 /64, not shared across the service.
    [Range(60, 100000)] public int ApiRequestsPerMinute { get; set; } = 6000;
    [Range(5, 120)] public int GroupJoinRequestsPerMinute { get; set; } = 20;
    [Range(60, 10000)] public int LoginRequestsPerMinute { get; set; } = 1200;
    [Range(1, 100)] public int OwnedCommunities { get; set; } = 3;
    [Range(1, 10000)] public int MaxRetainedCreatedCommunitiesPerCharacter { get; set; } = 30;
    [Range(1, 100)] public int JoinedCommunities { get; set; } = 6;
    [Range(0, 10000)] public int DefaultLettersPerWeek { get; set; } = 5;
    [Range(0, 1000000)] public int DefaultPotionPoints { get; set; }
    [Range(1, 500)] public int ChatMessageTextElements { get; set; } = 50;
    [Range(1, 1000)] public int DefinitionNameTextElements { get; set; } = 80;
    [Range(1, 300)] public int DefinitionDescriptionTextElements { get; set; } = 300;
    [Range(1, 1000)] public int LetterTitleTextElements { get; set; } = 80;
    [Range(1, 500)] public int LetterBodyTextElements { get; set; } = 500;
    [Range(1, 1000)] public int MaxPageSize { get; set; } = 100;
    [Range(1, 1000000)] public int MaxHoldingsPerCharacterCommunity { get; set; } = 10000;
    [Range(1, 1000)] public int MaxTradeLinesPerSide { get; set; } = 20;
    [Range(1, 10000)] public int MaxCreationQuantity { get; set; } = 100;
    [Range(1, 86400)] public int TradeLifetimeSeconds { get; set; } = 600;
    [Range(1024, 1048576)] public int MaxRequestBytes { get; set; } = 65536;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (JoinedCommunities < OwnedCommunities)
            yield return new ValidationResult("JoinedCommunities must include owned communities.", [nameof(JoinedCommunities)]);
        if (MaxRetainedCreatedCommunitiesPerCharacter < OwnedCommunities)
            yield return new ValidationResult("Retained created-community budget must include the active owned-community allowance.", [nameof(MaxRetainedCreatedCommunitiesPerCharacter)]);
    }

    public PublicLimits ToContract()
    {
        var communities = new CommunityCapacityPolicy(OwnedCommunities, JoinedCommunities);
        return new PublicLimits(communities.OwnedCommunities, communities.JoinedCommunities,
            DefaultLettersPerWeek, DefaultPotionPoints, ChatMessageTextElements,
            LetterTitleTextElements, LetterBodyTextElements, MaxPageSize,
            MaxHoldingsPerCharacterCommunity, MaxTradeLinesPerSide, MaxCreationQuantity,
            TradeLifetimeSeconds, "Monday", "00:00:00", MaxRequestBytes, MaxWeeklyPotionPoints, MaxWeeklyLetters, MaxActiveInvitationsPerCommunity, OwnershipProposalHours, MaxCurrencyIconBytes, 128, MaxCategoriesPerCommunity, MaxDefinitionsPerCommunity, DefinitionNameTextElements, DefinitionDescriptionTextElements, MaxCommunityLogoBytes, 2048, 512);
    }
}
