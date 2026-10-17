using AmorRP.Contracts.Inventory;
namespace AmorRP.Server.Features.Catalog;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/communities/{communityId}/categories", (HttpContext h, Guid communityId, CatalogService s, CancellationToken ct) => s.CategoriesAsync(h, communityId, null, ct));
        app.MapGet("/api/v2/communities/{communityId}/categories/{categoryId}", (HttpContext h, Guid communityId, Guid categoryId, CatalogService s, CancellationToken ct) => s.CategoriesAsync(h, communityId, categoryId, ct));
        app.MapPost("/api/v2/communities/{communityId}/categories", (HttpContext h, Guid communityId, CategoryEdit b, CatalogService s, CancellationToken ct) => s.CategoryCommandAsync(h, communityId, null, b, ct));
        app.MapPatch("/api/v2/communities/{communityId}/categories/{categoryId}", (HttpContext h, Guid communityId, Guid categoryId, CategoryEdit b, CatalogService s, CancellationToken ct) => s.CategoryCommandAsync(h, communityId, categoryId, b, ct));
        app.MapPost("/api/v2/communities/{communityId}/categories/{categoryId}/retirement", (HttpContext h, Guid communityId, Guid categoryId, CatalogService s, CancellationToken ct) => s.CategoryCommandAsync(h, communityId, categoryId, null, ct));
        app.MapGet("/api/v2/communities/{communityId}/item-definitions", (HttpContext h, Guid communityId, CatalogService s, CancellationToken ct) => s.DefinitionsAsync(h, communityId, null, null, ct));
        app.MapGet("/api/v2/communities/{communityId}/item-definitions/{definitionId}", (HttpContext h, Guid communityId, Guid definitionId, int? revision, CatalogService s, CancellationToken ct) => s.DefinitionsAsync(h, communityId, definitionId, revision, ct));
        app.MapPost("/api/v2/communities/{communityId}/item-definitions", (HttpContext h, Guid communityId, DefinitionWrite b, CatalogService s, CancellationToken ct) => s.DefinitionCommandAsync(h, communityId, null, b, ct));
        app.MapPatch("/api/v2/communities/{communityId}/item-definitions/{definitionId}", (HttpContext h, Guid communityId, Guid definitionId, DefinitionWrite b, CatalogService s, CancellationToken ct) => s.DefinitionCommandAsync(h, communityId, definitionId, b, ct));
        app.MapPost("/api/v2/communities/{communityId}/item-definitions/{definitionId}/retirement", (HttpContext h, Guid communityId, Guid definitionId, CatalogService s, CancellationToken ct) => s.DefinitionCommandAsync(h, communityId, definitionId, null, ct));
    }
}
