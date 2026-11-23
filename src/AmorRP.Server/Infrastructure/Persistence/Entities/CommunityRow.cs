namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class CommunityRow
{
    public int ClassSlots { get; set; } = 1;
    public long ClassResetPrice { get; set; } = 1000;
    public string ClassCatalog { get; set; } = "[]";
    public int ClassPolicyVersion { get; set; } = 1;
    public int GroupRecipeLimit { get; set; } = 10;
    public string? OwnershipEffectJson { get; set; }
    public int OwnershipEffectVersion { get; set; } = 1;
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string SeasonState { get; set; } = "draft";
    public Guid OwnerCharacterId { get; set; }
    public Guid CreatedByCharacterId { get; set; }
    public long AnnouncementSequence { get; set; }
    public int InventoryVersion { get; set; } = 1;
    public int Version { get; set; } = 1;
    public bool Deleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? LogoAssetId { get; set; }
    public int LogoVersion { get; set; } = 1;
    public bool AllowDebt { get; set; }
    public int BillingPolicyVersion { get; set; } = 1;
    public int BillingTaxPercent { get; set; }
    public long SeasonTreasuryOwned { get; set; }
    public long PermitPrice { get; set; } = 1;
    public bool PermitEnabled { get; set; }
    public Guid CurrencyId { get; set; } = Guid.CreateVersion7();
    public string CurrencyName { get; set; } = "";
    public string CurrencySymbol { get; set; } = "";
    public Guid? CurrencyIconAssetId { get; set; }
    public int CurrencyVersion { get; set; } = 1;
    public int PolicyVersion { get; set; } = 1;
    public int CurrentPotionPoints { get; set; }
    public int GatheringVersion { get; set; }
    public int CurrentGatheringPoints { get; set; }
    public int NextGatheringPoints { get; set; }
    public int CurrentCookingPoints { get; set; }
    public int NextCookingPoints { get; set; }
    public int CurrentLetters { get; set; }
    public int NextPotionPoints { get; set; }
    public int NextLetters { get; set; }
    public DateTimeOffset PolicyPeriod { get; set; }
}
