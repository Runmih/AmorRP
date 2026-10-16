using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Groups;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using Xunit;
namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    private async Task<HttpResponseMessage> UploadGroupImage(HttpClient client, string path, byte[] bytes, string etag, Guid? key = null)
    {
        using var request = Request(HttpMethod.Put, path, key: key, etag: etag);
        var content = new MultipartFormDataContent(); content.Add(new ByteArrayContent(bytes), "file", "untrusted.extension"); request.Content = content;
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
    private static byte[] LogoBytes(int width, int height, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(width, height); bitmap.Erase(SKColors.Teal);
        using var image = bitmap.Encode(format, 90); return image.ToArray();
    }
    private static string LogoTag(Group group) => $"\"group-logo:{group.Id:N}:{group.LogoVersion}\"";

    [Theory]
    [InlineData(SKEncodedImageFormat.Png)] [InlineData(SKEncodedImageFormat.Jpeg)] [InlineData(SKEncodedImageFormat.Webp)]
    public async Task Logos_downscale_persist_replay_and_remain_independent_of_currency_and_profile(SKEncodedImageFormat format)
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(); await Migrate(factory); using var owner = factory.CreateClient(); var token = await Login(owner, "800"); var group = await Create(owner);
        var key = Guid.CreateVersion7(clock.GetUtcNow()); var path = $"/api/v1/groups/{group.Id}/logo"; var bytes = LogoBytes(1200, 600, format);
        using var upload = await UploadGroupImage(owner, path, bytes, LogoTag(group), key); Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var saved = (await upload.Content.ReadFromJsonAsync<CommandResult<Group>>(ct))!.Result;
        Assert.NotNull(saved.LogoAssetId); Assert.Equal(2, saved.LogoVersion); Assert.Equal(group.Version, saved.Version); Assert.Equal(group.Currency, saved.Currency);
        using var retry = await UploadGroupImage(owner, path, bytes, LogoTag(group), key);
        Assert.True(retry.Headers.Contains("Idempotency-Replayed")); Assert.Equal(saved.LogoAssetId, (await retry.Content.ReadFromJsonAsync<CommandResult<Group>>(ct))!.Result.LogoAssetId);
        var png = await owner.GetByteArrayAsync(path, ct); using (var bitmap = SKBitmap.Decode(png)) { Assert.Equal(512, bitmap.Width); Assert.Equal(256, bitmap.Height); }
        using var response = await owner.GetAsync(path, ct); Assert.Equal("no-store", response.Headers.CacheControl!.ToString()); Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        using var restarted = Factory(); using var after = restarted.CreateClient(); after.DefaultRequestHeaders.Authorization = new("Bearer", token.AccessToken);
        Assert.Equal(png, await after.GetByteArrayAsync(path, ct));
        // A profile save does not invalidate the independent logo ETag.
        var renamed = await Command<Group>(owner, HttpMethod.Patch, $"/api/v1/groups/{group.Id}", new GroupEdit("New group name", "Profile"), $"\"group:{group.Id:N}:{group.Version}\"");
        using var replacement = await UploadGroupImage(owner, path, LogoBytes(256, 512), LogoTag(saved)); Assert.Equal(HttpStatusCode.OK, replacement.StatusCode);
        var changed = (await replacement.Content.ReadFromJsonAsync<CommandResult<Group>>(ct))!.Result;
        Assert.Equal(renamed.Name, changed.Name); Assert.Equal(3, changed.LogoVersion);
        using var currency = await UploadGroupImage(owner, $"/api/v1/groups/{group.Id}/currency/icon", LogoBytes(128, 128), $"\"currency:{group.Currency.Id:N}:1\""); Assert.Equal(HttpStatusCode.OK, currency.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
            Assert.Equal(2, await db.MediaAssets.CountAsync(ct)); Assert.Single(await db.MediaAssets.Where(x => x.Purpose == "group_logo").ToArrayAsync(ct));
            Assert.False(await db.MediaAssets.AnyAsync(x => x.Id == saved.LogoAssetId, ct));
        }
        var removed = await Command<Group>(owner, HttpMethod.Delete, path, etag: LogoTag(changed)); Assert.Null(removed.LogoAssetId);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(path, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/v1/groups/{group.Id}/currency/icon", ct)).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope()) Assert.Equal("currency", (await scope.ServiceProvider.GetRequiredService<AmorDbContext>().MediaAssets.SingleAsync(ct)).Purpose);
    }

    [Fact]
    public async Task Logo_uploads_enforce_owner_active_membership_stale_versions_and_image_bounds()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(); await Migrate(factory); using var owner = factory.CreateClient(); await Login(owner, "801"); var group = await Create(owner);
        using var member = factory.CreateClient(); var memberId = (await Login(member, "802")).Character.Id; await Join(member, await Invite(owner, group));
        using var outsider = factory.CreateClient(); await Login(outsider, "803");
        await GrantHome(owner, group, memberId, "group.moderate", "announcements.manage");
        var path = $"/api/v1/groups/{group.Id}/logo"; var image = LogoBytes(128, 128);
        using (var denied = await UploadGroupImage(member, path, [1, 2, 3], LogoTag(group))) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var hidden = await UploadGroupImage(outsider, path, image, LogoTag(group))) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using (var uploaded = await UploadGroupImage(owner, path, image, LogoTag(group))) Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        var current = (await owner.GetFromJsonAsync<Group>($"/api/v1/groups/{group.Id}", ct))!;
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync(path, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(path, ct)).StatusCode);
        using (var stale = await UploadGroupImage(owner, path, image, LogoTag(group))) Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        using (var invalid = await UploadGroupImage(owner, path, [1, 2, 3, 4], LogoTag(current))) Assert.Equal(HttpStatusCode.UnsupportedMediaType, invalid.StatusCode);
        using (var oversized = await UploadGroupImage(owner, path, new byte[2097153], LogoTag(current))) Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        using (var dimensions = await UploadGroupImage(owner, path, LogoBytes(2049, 1), LogoTag(current))) Assert.Equal(HttpStatusCode.UnprocessableEntity, dimensions.StatusCode);
        var membership = await Member(owner, group, memberId);
        await Command<Member>(member, HttpMethod.Delete, $"/api/v1/groups/{group.Id}/members/{memberId}", new MemberRemoval("Leave"), membership.Etag);
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync(path, ct)).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.Single(await db.MediaAssets.ToArrayAsync(ct)); Assert.Equal(2, (await db.Groups.SingleAsync(ct)).LogoVersion);
    }

    [Fact]
    public async Task Logo_migration_preserves_old_currency_images_and_group_identity()
    {
        using var factory = Factory(); await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>(); var ct = TestContext.Current.CancellationToken;
        await db.GetService<IMigrator>().MigrateAsync("20261015232913_GroupAnnouncements", ct);
        var owner = Guid.NewGuid(); var group = Guid.NewGuid(); var currency = Guid.NewGuid(); var asset = Guid.NewGuid(); var now = clock.GetUtcNow(); var png = LogoBytes(128, 64);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO characters ("Id", "LodestoneId", "OwnershipKeyHash", "DisplayName", "HomeWorldId", "HomeWorldName", "VerifiedAt")
                VALUES ({owner}, '804', 'binding', 'Owner', '35', 'Zalera', {now});
            INSERT INTO groups ("Id", "CreatedByCharacterId", "Name", "Description", "OwnerCharacterId", "Version", "Deleted", "CurrencyId", "CurrencyName", "CurrencySymbol", "CurrencyVersion", "PolicyVersion", "CurrentPotionPoints", "CurrentLetters", "NextPotionPoints", "NextLetters", "PolicyPeriod", "InventoryVersion", "AnnouncementSequence")
                VALUES ({group}, {owner}, 'Preserved group', 'Description', {owner}, 7, false, {currency}, 'Coins', 'C', 3, 1, 5, 5, 5, 5, {now}, 9, 0);
            INSERT INTO media_assets ("GroupId", "Id", "Png", "ContentHash", "Width", "Height") VALUES ({group}, {asset}, {png}, 'hash', 128, 64);
            UPDATE groups SET "CurrencyIconAssetId" = {asset} WHERE "Id" = {group};
            """, ct);
        await db.Database.MigrateAsync(ct); db.ChangeTracker.Clear();
        var upgraded = await db.Groups.SingleAsync(ct); var preserved = await db.MediaAssets.SingleAsync(ct);
        Assert.Equal("Preserved group", upgraded.Name); Assert.Equal(7, upgraded.Version); Assert.Equal(currency, upgraded.CurrencyId); Assert.Equal(asset, upgraded.CurrencyIconAssetId);
        Assert.Null(upgraded.LogoAssetId); Assert.Equal(1, upgraded.LogoVersion); Assert.Equal("currency", preserved.Purpose); Assert.Equal(png, preserved.Png);
    }
}
