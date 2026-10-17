namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class MediaAssetRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CommunityId { get; set; }
    public string Purpose { get; set; } = "currency";
    public byte[] Png { get; set; } = [];
    public string ContentHash { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
}
