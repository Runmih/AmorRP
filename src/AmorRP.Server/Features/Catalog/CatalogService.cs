using AmorRP.Contracts.Common;
using AmorRP.Contracts.Inventory;
using AmorRP.Core.Items;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Combat;
using AmorRP.Server.Features.Seasons;
using AmorRP.Server.Features.Moodles;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Catalog;

public sealed class CatalogService(AmorDbContext db, SessionAccess sessions, SeasonAccess communities,
    CommandStore commands, PageCursor cursors, IOptions<ServicePolicyOptions> policy, StatusEffectService effects)
{
    public static Category View(CategoryRow row) => new(row.Id, row.CommunityId, row.Name, row.AllowedTypeIds, row.Retired, row.Version);
    public static Definition View(DefinitionRow row, DefinitionRevisionRow rev) => new(row.Id, row.CommunityId, row.TypeId,
        rev.Revision, rev.TypeDataVersion, rev.Name, rev.CategoryId, rev.Description, rev.Enabled, row.Retired, new(rev.CreationCost, rev.UseMessage), rev.BonusPoints, row.GroupId, StatusEffectService.Decode(rev.StatusEffectJson), CombatService.Gear(rev.GearJson), rev.StatPoints);
    public static void Seed(AmorDbContext db, Guid communityId)
    {
        db.Categories.AddRange(new CategoryRow { CommunityId = communityId, Name = "Alchemy", AllowedTypeIds = CraftingTypes.Alchemy },
            new CategoryRow { CommunityId = communityId, Name = "Cooking", AllowedTypeIds = CraftingTypes.Cooking },
            new CategoryRow { CommunityId = communityId, Name = "Blacksmithing", AllowedTypeIds = CraftingTypes.Blacksmithing },
            new CategoryRow { CommunityId = communityId, Name = "Books", AllowedTypeIds = ["book", "letter", "note"] });
    }
    public async Task<CategoryRow> RequireCategoryAsync(Guid communityId, Guid id, string type, CancellationToken ct)
    {
        var row = await db.Categories.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.Id == id, ct);
        ApiFault.Require(row != null && !row.Retired && row.AllowedTypeIds.Contains(type), 422, "invalid_category", "Choose an active category that accepts this item type.");
        return row!;
    }
    public async Task<IResult> CategoriesAsync(HttpContext h, Guid communityId, Guid? id, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId; var (community, _) = await communities.RequireAsync(communityId, actor, ct);
        if (id.HasValue)
        {
            var row = await db.Categories.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.Id == id, ct);
            ApiFault.Require(row != null, 404, "not_found", "Category not found."); h.Response.Headers.ETag = CommandStore.ETag("category", row!.Id, row.Version); return Results.Json(View(row));
        }
        var scope = $"categories:{communityId}:{actor}"; var (limit, after) = cursors.Read(h, scope);
        var query = db.Categories.Where(x => x.CommunityId == communityId);
        if (after.HasValue) query = query.Where(x => x.Id.CompareTo(after.Value) > 0);
        var rows = await query.OrderBy(x => x.Id).Take(limit + 1).ToArrayAsync(ct);
        return Results.Json(new CategoryPage(rows.Take(limit).Select(View).ToArray(), rows.Length > limit ? cursors.Write(rows[limit - 1].Id, scope) : null, community.InventoryVersion.ToString()));
    }
    public async Task<IResult> CategoryCommandAsync(HttpContext h, Guid communityId, Guid? id, CategoryEdit? body, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId; await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "season:" + communityId], ct);
        var (community, _) = await communities.RequireAsync(communityId, actor, ct); SeasonAccess.Owner(community, actor);
        var scope = "character:" + actor; var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        ApiFault.Require(await db.Categories.CountAsync(x => x.CommunityId == communityId, ct) < policy.Value.MaxCategoriesPerCommunity || id.HasValue, 409, "catalog_full", "Category limit reached.");
        var row = id.HasValue ? await db.Categories.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.Id == id, ct) : new CategoryRow { CommunityId = communityId };
        ApiFault.Require(row != null, 404, "not_found", "Category not found.");
        if (id.HasValue) CommandStore.Match(h, CommandStore.ETag("category", row!.Id, row.Version));
        if (body != null)
        {
            ApiFault.Text(body.Name, 80); ApiFault.Require(body.AllowedTypeIds is { Length: > 0 } && body.AllowedTypeIds.Length <= CraftingTypes.All.Length && body.AllowedTypeIds.All(x => CraftingTypes.All.Contains(x)) && body.AllowedTypeIds.Distinct().Count() == body.AllowedTypeIds.Length, 422, "invalid_request", "Select supported item types.");
            ApiFault.Require(!row!.Retired, 409, "category_retired", "Retired categories cannot be edited.");
            row.Name = body.Name.Trim(); row.AllowedTypeIds = body.AllowedTypeIds;
        }
        else row!.Retired = true;
        if (id.HasValue) row!.Version++; else db.Categories.Add(row!);
        community.InventoryVersion++;
        var op = commands.New(h, scope, body, actor, communityId, id == null ? "category.create" : body == null ? "category.retire" : "category.edit");
        op.Summary = $"{op.Kind}: {row!.Name}."; h.Response.Headers.ETag = CommandStore.ETag("category", row.Id, row.Version);
        var result = await commands.SaveAsync(h, op, new CommandResult<Category>(op.Id, View(row)), ct); await tx.CommitAsync(ct); return result;
    }
    public async Task<IResult> DefinitionsAsync(HttpContext h, Guid communityId, Guid? id, int? revision, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (community, member) = await communities.RequireAsync(communityId, actor, ct);
        var groups = GroupRecipes.Memberships(db, communityId, actor);
        var visibleTypes = CraftingTypes.All.Where(CraftingTypes.IsRecipe).Where(type =>
            SeasonAccess.HasAction(community, member, CraftingTypes.CraftPermission(type)) ||
            SeasonAccess.HasAction(community, member, CraftingTypes.RecipePermission(type))).ToArray();
        var query = db.Definitions.Where(x => x.CommunityId == communityId &&
            (x.GroupId == null || (groups.Contains(x.GroupId.Value) && visibleTypes.Contains(x.TypeId))));
        if (id.HasValue)
        {
            var row = await query.SingleOrDefaultAsync(x => x.Id == id, ct);
            ApiFault.Require(row != null, 404, "not_found", "Definition not found.");
            var rev = await db.DefinitionRevisions.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.DefinitionId == id && x.Revision == (revision ?? row!.Revision), ct);
            ApiFault.Require(rev != null, 404, "not_found", "Definition revision not found.");
            h.Response.Headers.ETag = CommandStore.ETag("definition", row!.Id, row.Revision); return Results.Json(View(row, rev!));
        }
        var scope = $"definitions:{communityId}:{actor}"; var (limit, after) = cursors.Read(h, scope);
        if (after.HasValue) query = query.Where(x => x.Id.CompareTo(after.Value) > 0);
        var rows = await query.OrderBy(x => x.Id).Take(limit + 1).ToArrayAsync(ct); var items = new List<Definition>();
        foreach (var row in rows.Take(limit)) items.Add(View(row, await db.DefinitionRevisions.SingleAsync(x => x.CommunityId == communityId && x.DefinitionId == row.Id && x.Revision == row.Revision, ct)));
        return Results.Json(new DefinitionPage(items.ToArray(), rows.Length > limit ? cursors.Write(rows[limit - 1].Id, scope) : null, community.InventoryVersion.ToString()));
    }
    public async Task<IResult> DefinitionCommandAsync(HttpContext h, Guid communityId, Guid? id, DefinitionWrite? body, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId; await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "season:" + communityId], ct);
        var (community, member) = await communities.RequireAsync(communityId, actor, ct);
        var row = id.HasValue ? await db.Definitions.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.Id == id, ct) : new DefinitionRow { CommunityId = communityId };
        ApiFault.Require(row != null, 404, "not_found", "Definition not found.");
        var groupId = id.HasValue ? row!.GroupId : body?.GroupId;
        await GroupRecipes.RequireAsync(db, communityId, actor, groupId, ct);
        ApiFault.Require(body == null || !id.HasValue || body.GroupId == row!.GroupId, 422, "recipe_scope", "A recipe's group cannot change.");
        ApiFault.Require(row == null || !CraftingTypes.IsSupply(row.TypeId), 422, "invalid_type", "Use Gathering settings for supply packages.");
        SeasonAccess.Action(community, member, CraftingTypes.RecipePermission(id.HasValue ? row!.TypeId : body?.TypeId ?? "potion"));
        var scope = "character:" + actor; var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        ApiFault.Require(id.HasValue || await db.Definitions.CountAsync(x => x.CommunityId == communityId, ct) < policy.Value.MaxDefinitionsPerCommunity, 409, "catalog_full", "Definition limit reached.");
        if (id.HasValue) CommandStore.Match(h, CommandStore.ETag("definition", row!.Id, row.Revision));
        ApiFault.Require(!row!.Retired, 409, "definition_retired", "Definition is retired.");
        DefinitionRevisionRow rev;
        if (body != null)
        {
            ApiFault.Require(CraftingTypes.IsRecipe(body.TypeId) && (!id.HasValue || row!.TypeId == body.TypeId) && body.Potion != null && body.Potion.CreationCost > 0, 422, "invalid_request", "Choose a recipe item type and a positive point cost.");
            var nameError = AmorRP.Core.Items.PlainText.Error(body.Name, policy.Value.DefinitionNameTextElements);
            ApiFault.Require(!string.Equals(body.Name?.Trim(), "Character Reset Potion", StringComparison.OrdinalIgnoreCase),
                422, "reserved_item_name", "Character Reset Potion is a built-in shop product.");
            var descriptionError = AmorRP.Core.Items.PlainText.Error(body.Description, policy.Value.DefinitionDescriptionTextElements, multiline: true, required: false);
            ApiFault.Require(nameError == null && descriptionError == null, 422, "invalid_request", nameError != null ? "Item name: " + nameError : "Item description: " + descriptionError);
            var messageError = CraftingTypes.IsGear(body.TypeId) ? null : AmorRP.Core.Items.LiteralChatText.Error(body.Potion!.UseMessage, policy.Value.ChatMessageTextElements);
            ApiFault.Require(messageError == null, 422, "invalid_message", messageError ?? "Invalid potion message.");
            var gear = CombatService.EncodeGear(body.TypeId, body.Gear);
            ApiFault.Require(body.StatPoints == 0 || (body.TypeId == "potion" && body.StatPoints is >= 1 and <= 10),
                422, "invalid_stat_effect", "Stat point effects belong to potions and grant 1 to 10 points.");
            ApiFault.Require(!CraftingTypes.IsGear(body.TypeId) || body.StatusEffect == null, 422,
                "invalid_gear_effect", "Only consumable recipes can apply a status effect.");
            var effect = await effects.FindAsync(communityId, body.StatusEffect, ct);
            await RequireCategoryAsync(communityId, body.CategoryId, body.TypeId, ct);
            if (!id.HasValue && groupId is { } group)
                ApiFault.Require(await db.Definitions.CountAsync(x => x.CommunityId == communityId && x.GroupId == group && !x.Retired, ct) < community.GroupRecipeLimit,
                    409, "group_recipe_limit", "This group has reached its recipe limit.");
            row.GroupId = groupId;
            row.TypeId = body.TypeId;
            if (id.HasValue) row.Revision++;
            rev = new() { CommunityId = communityId, DefinitionId = row.Id, Revision = row.Revision, Name = body.Name!.Trim(), CategoryId = body.CategoryId, Description = body.Description ?? "", Enabled = body.Enabled, CreationCost = body.Potion!.CreationCost, UseMessage = CraftingTypes.IsGear(body.TypeId) ? "" : body.Potion.UseMessage, StatusEffectJson = effect == null ? null : StatusEffectService.Encode(effect), GearJson = gear, StatPoints = body.StatPoints };
            db.DefinitionRevisions.Add(rev); if (!id.HasValue) db.Definitions.Add(row);
        }
        else
        {
            var previous = await db.DefinitionRevisions.SingleAsync(x => x.CommunityId == communityId && x.DefinitionId == row.Id && x.Revision == row.Revision, ct);
            row.Retired = true; row.Revision++;
            rev = new() { CommunityId = communityId, DefinitionId = row.Id, Revision = row.Revision, Name = previous.Name, CategoryId = previous.CategoryId, Description = previous.Description, Enabled = false, CreationCost = previous.CreationCost, UseMessage = previous.UseMessage, StatusEffectJson = previous.StatusEffectJson, GearJson = previous.GearJson, StatPoints = previous.StatPoints };
            db.DefinitionRevisions.Add(rev);
        }
        community.InventoryVersion++;
        var op = commands.New(h, scope, body, actor, communityId, id == null ? "definition.create" : body == null ? "definition.retire" : "definition.revise");
        op.Summary = row.GroupId != null ? $"{op.Kind}: group recipe, revision {rev.Revision}." : $"{op.Kind}: {rev.Name}, revision {rev.Revision}."; h.Response.Headers.ETag = CommandStore.ETag("definition", row.Id, row.Revision);
        var result = await commands.SaveAsync(h, op, new CommandResult<Definition>(op.Id, View(row, rev)), ct); await tx.CommitAsync(ct); return result;
    }
}
