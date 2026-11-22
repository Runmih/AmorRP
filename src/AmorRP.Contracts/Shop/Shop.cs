using AmorRP.Contracts.Inventory;

namespace AmorRP.Contracts.Shop;

public sealed record ShopProduct(Guid Id, Guid DefinitionId, int DefinitionRevision, string Name, string Description,
    string Price, string Buyer, int? Stock, int? BuyerLimit, bool Listed, int Version, string Etag);
public sealed record ShopProductWrite(Guid DefinitionId, string Price, string Buyer, int? Stock, int? BuyerLimit, bool Listed);
public sealed record ShopProducts(ShopProduct[] Products);
public sealed record ShopBuy(Guid ProductId, int ExpectedVersion, int Quantity, Guid? GroupId = null);
public sealed record ShopPurchase(Guid Id, Guid ProductId, string Name, int Quantity, string Total, Guid? GroupId, Holding Delivery);
