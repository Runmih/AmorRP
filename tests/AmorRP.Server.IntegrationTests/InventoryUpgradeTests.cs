using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task Pairing_upgrade_retires_old_browser_handoffs_and_preserves_saved_sessions()
    {
        using var factory = Factory(); await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>(); var ct = TestContext.Current.CancellationToken;
        await db.GetService<IMigrator>().MigrateAsync("20261015015854_DoubleTradeConfirmation", ct);
        var actor = Guid.NewGuid(); var session = Guid.NewGuid(); var pending = Guid.NewGuid(); var verified = Guid.NewGuid(); var now = clock.GetUtcNow(); var expiry = now.AddDays(5);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO characters ("Id", "LodestoneId", "OwnershipKeyHash", "DisplayName", "HomeWorldId", "HomeWorldName", "VerifiedAt") VALUES ({actor}, '995', 'test-binding', 'Existing owner', '35', 'Zalera', {now});
            INSERT INTO sessions ("Id", "CharacterId", "AccessHash", "AccessExpiresAt", "RefreshHash", "RefreshExpiresAt", "ProtectedProviderRefresh", "CreatedAt", "LastUsedAt", "Revoked") VALUES ({session}, {actor}, 'access-hash', {expiry}, 'refresh-hash', {expiry}, 'protected-existing-grant', {now}, {now}, false);
            INSERT INTO login_attempts ("Id", "CredentialHash", "StateHash", "ProtectedVerifier", "DisplayName", "HomeWorldId", "HomeWorldName", "Status", "ExpiresAt", "StateUsed", "ProtectedProviderRefresh", "CharacterId") VALUES
            ({pending}, 'attempt-a', 'state-a', 'old-pkce-a', 'Existing owner', '35', 'Zalera', 'pending', {expiry}, false, NULL, NULL),
            ({verified}, 'attempt-b', 'state-b', '', 'Existing owner', '35', 'Zalera', 'verified', {expiry}, true, 'old-unpaired-grant', {actor});
            """, ct);
        await db.Database.MigrateAsync(ct);
        var saved = await db.Sessions.SingleAsync(ct); Assert.Equal(session, saved.Id); Assert.False(saved.Revoked);
        Assert.Equal("refresh-hash", saved.RefreshHash); Assert.Equal("protected-existing-grant", saved.ProtectedProviderRefresh);
        var attempts = await db.Logins.ToArrayAsync(ct); Assert.Equal(2, attempts.Length);
        Assert.All(attempts, row => { Assert.Equal("cancelled", row.Status); Assert.Empty(row.ProtectedVerifier); Assert.Null(row.ProtectedProviderRefresh); Assert.Null(row.PairingCodeHash); });
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task M2_upgrade_seeds_categories_and_preserves_existing_currency_and_membership()
    {
        using var f = Factory(); await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        var ct = TestContext.Current.CancellationToken;
        await db.GetService<IMigrator>().MigrateAsync("20261007075330_GroupFoundation", ct);
        var actor = Guid.NewGuid(); var community = Guid.NewGuid(); var currency = Guid.NewGuid(); var now = clock.GetUtcNow();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO characters ("Id", "LodestoneId", "OwnershipKeyHash", "DisplayName", "HomeWorldId", "HomeWorldName", "VerifiedAt")
            VALUES ({actor}, '999', 'test-binding', 'Existing owner', '35', 'Zalera', {now});
            INSERT INTO groups ("Id", "Name", "Description", "OwnerCharacterId", "Version", "Deleted", "CurrencyId", "CurrencyName", "CurrencySymbol", "CurrencyVersion", "PolicyVersion", "CurrentPotionPoints", "CurrentLetters", "NextPotionPoints", "NextLetters", "PolicyPeriod")
            VALUES ({community}, 'Existing community', '', {actor}, 1, false, {currency}, 'Existing coins', 'C', 1, 1, 12, 5, 12, 5, {now});
            INSERT INTO memberships ("GroupId", "CharacterId", "Status", "Capabilities", "TradingRestricted", "Version", "Owned", "Reserved", "BalanceVersion")
            VALUES ({community}, {actor}, 'active', ARRAY[]::text[], false, 1, 12345, 0, 1);
            """, ct);
        await db.Database.MigrateAsync(ct);
        Assert.Equal(currency, (await db.Communities.SingleAsync(ct)).CurrencyId);
        Assert.Equal(12345, (await db.Memberships.SingleAsync(ct)).Owned);
        Assert.Equal(new[] { "Consumables", "Correspondence" }, await db.Categories.OrderBy(x => x.Name).Select(x => x.Name).ToArrayAsync(ct));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(ct));
    }
    [Fact]
    public async Task M3_upgrade_preserves_letter_identity_contents_allowance_and_currency()
    {
        using var f = Factory(); await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        var ct = TestContext.Current.CancellationToken;
        await db.GetService<IMigrator>().MigrateAsync("20261007165023_PlayableInventory", ct);
        var actor = Guid.NewGuid(); var community = Guid.NewGuid(); var currency = Guid.NewGuid(); var category = Guid.NewGuid(); var letter = Guid.NewGuid(); var holding = Guid.NewGuid(); var now = clock.GetUtcNow();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO characters ("Id", "LodestoneId", "OwnershipKeyHash", "DisplayName", "HomeWorldId", "HomeWorldName", "VerifiedAt") VALUES ({actor}, '998', 'test-binding', 'M3 owner', '35', 'Zalera', {now});
            INSERT INTO groups ("Id", "Name", "Description", "OwnerCharacterId", "Version", "Deleted", "CurrencyId", "CurrencyName", "CurrencySymbol", "CurrencyVersion", "PolicyVersion", "CurrentPotionPoints", "CurrentLetters", "NextPotionPoints", "NextLetters", "PolicyPeriod", "InventoryVersion") VALUES ({community}, 'M3 community', '', {actor}, 1, false, {currency}, 'M3 coins', 'C', 1, 1, 12, 5, 12, 5, {now}, 3);
            INSERT INTO memberships ("GroupId", "CharacterId", "Status", "Capabilities", "TradingRestricted", "Version", "Owned", "Reserved", "BalanceVersion") VALUES ({community}, {actor}, 'active', ARRAY['items.potion.create']::text[], false, 1, 456, 0, 1);
            INSERT INTO categories ("GroupId", "Id", "Name", "AllowedTypeIds", "Retired", "Version") VALUES ({community}, {category}, 'Correspondence', ARRAY['letter']::text[], false, 1);
            INSERT INTO letters ("GroupId", "Id", "AuthorCharacterId", "CategoryId", "Title", "Body", "Version") VALUES ({community}, {letter}, {actor}, {category}, 'Retained letter', 'Original private text', 2);
            INSERT INTO holdings ("GroupId", "Id", "CharacterId", "CategoryId", "TypeId", "Name", "LetterId", "Quantity", "Reserved", "Version") VALUES ({community}, {holding}, {actor}, {category}, 'letter', 'Retained letter', {letter}, 1, 0, 2);
            INSERT INTO quota_usage ("GroupId", "CharacterId", "Kind", "PeriodStart", "Limit", "Used") VALUES ({community}, {actor}, 'letters', {now}, 5, 3);
            """, ct);
        await db.Database.MigrateAsync(ct);
        var document = await db.Letters.SingleAsync(ct); Assert.Equal(letter, document.Id); Assert.Equal("letter", document.TypeId); Assert.Equal("Original private text", document.Body); Assert.Equal(2, document.Version);
        var item = await db.Holdings.SingleAsync(ct); Assert.Equal(holding, item.Id); Assert.Equal(actor, item.CharacterId); Assert.Equal(letter, item.LetterId);
        var retained = await db.Categories.SingleAsync(ct); Assert.Equal(category, retained.Id); Assert.Equal("Correspondence", retained.Name);
        Assert.Equal(new[] { "letter" }, retained.AllowedTypeIds); Assert.Equal(1, retained.Version);
        Assert.Equal(3, (await db.Quotas.SingleAsync(ct)).Used); Assert.Equal(456, (await db.Memberships.SingleAsync(ct)).Owned); Assert.Equal(currency, (await db.Communities.SingleAsync(ct)).CurrencyId);
    }

    [Fact]
    public async Task M4_upgrade_preserves_reserved_offer_and_requires_new_approval()
    {
        using var f = Factory(); await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        var ct = TestContext.Current.CancellationToken;
        await db.GetService<IMigrator>().MigrateAsync("20261014230954_TradingBeta", ct);
        var actor = Guid.NewGuid(); var other = Guid.NewGuid(); var community = Guid.NewGuid(); var currency = Guid.NewGuid(); var trade = Guid.NewGuid(); var now = clock.GetUtcNow();
        // PostgreSQL timestamps have microsecond precision, so seed a representable expiry.
        var expires = now.AddMinutes(10); expires = expires.AddTicks(-(expires.Ticks % 10));
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO characters ("Id", "LodestoneId", "OwnershipKeyHash", "DisplayName", "HomeWorldId", "HomeWorldName", "VerifiedAt") VALUES
            ({actor}, '996', 'test-binding-a', 'M4 owner', '35', 'Zalera', {now}), ({other}, '997', 'test-binding-b', 'M4 member', '35', 'Zalera', {now});
            INSERT INTO groups ("Id", "Name", "Description", "OwnerCharacterId", "Version", "Deleted", "CurrencyId", "CurrencyName", "CurrencySymbol", "CurrencyVersion", "PolicyVersion", "CurrentPotionPoints", "CurrentLetters", "NextPotionPoints", "NextLetters", "PolicyPeriod", "InventoryVersion") VALUES
            ({community}, 'M4 community', '', {actor}, 1, false, {currency}, 'Coins', 'C', 1, 1, 12, 5, 12, 5, {now}, 1);
            INSERT INTO memberships ("GroupId", "CharacterId", "Status", "Capabilities", "TradingRestricted", "Version", "Owned", "Reserved", "BalanceVersion") VALUES
            ({community}, {actor}, 'active', ARRAY[]::text[], false, 1, 50, 20, 2), ({community}, {other}, 'active', ARRAY[]::text[], false, 1, 0, 0, 1);
            INSERT INTO trades ("GroupId", "Id", "InitiatorId", "RecipientId", "State", "Revision", "InitiatorCurrency", "RecipientCurrency", "InitiatorConfirmed", "RecipientConfirmed", "ExpiresAt") VALUES
            ({community}, {trade}, {actor}, {other}, 'open', 3, 20, 0, true, false, {expires});
            INSERT INTO trade_slots ("GroupId", "CharacterId", "TradeId") VALUES ({community}, {actor}, {trade}), ({community}, {other}, {trade});
            """, ct);
        await db.Database.MigrateAsync(ct);
        var retained = await db.Trades.SingleAsync(ct); Assert.Equal(trade, retained.Id); Assert.Equal(4, retained.Revision); Assert.Equal("open", retained.State);
        Assert.Equal(20, retained.InitiatorCurrency); Assert.Equal(expires, retained.ExpiresAt);
        Assert.False(retained.InitiatorConfirmed); Assert.False(retained.RecipientConfirmed); Assert.False(retained.InitiatorFinalConfirmed); Assert.False(retained.RecipientFinalConfirmed);
        var owner = await db.Memberships.SingleAsync(x => x.CharacterId == actor, ct); Assert.Equal(50, owner.Owned); Assert.Equal(20, owner.Reserved);
        Assert.Equal(2, await db.TradeSlots.CountAsync(ct)); Assert.Equal(currency, (await db.Communities.SingleAsync(ct)).CurrencyId);
    }
}
