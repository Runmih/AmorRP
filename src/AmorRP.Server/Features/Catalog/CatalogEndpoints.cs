using AmorRP.Contracts.Inventory;
namespace AmorRP.Server.Features.Catalog;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/seasons/{seasonId}/categories", (HttpContext h, Guid seasonId, CatalogService s, CancellationToken ct) => s.CategoriesAsync(h, seasonId, null, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/categories/{categoryId}", (HttpContext h, Guid seasonId, Guid categoryId, CatalogService s, CancellationToken ct) => s.CategoriesAsync(h, seasonId, categoryId, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/categories", (HttpContext h, Guid seasonId, CategoryEdit b, CatalogService s, CancellationToken ct) => s.CategoryCommandAsync(h, seasonId, null, b, ct));
        app.MapPatch("/api/v2/seasons/{seasonId}/categories/{categoryId}", (HttpContext h, Guid seasonId, Guid categoryId, CategoryEdit b, CatalogService s, CancellationToken ct) => s.CategoryCommandAsync(h, seasonId, categoryId, b, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/categories/{categoryId}/retirement", (HttpContext h, Guid seasonId, Guid categoryId, CatalogService s, CancellationToken ct) => s.CategoryCommandAsync(h, seasonId, categoryId, null, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/item-definitions", (HttpContext h, Guid seasonId, CatalogService s, CancellationToken ct) => s.DefinitionsAsync(h, seasonId, null, null, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/item-definitions/{definitionId}", (HttpContext h, Guid seasonId, Guid definitionId, int? revision, CatalogService s, CancellationToken ct) => s.DefinitionsAsync(h, seasonId, definitionId, revision, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/item-definitions", (HttpContext h, Guid seasonId, DefinitionWrite b, CatalogService s, CancellationToken ct) => s.DefinitionCommandAsync(h, seasonId, null, b, ct));
        app.MapPatch("/api/v2/seasons/{seasonId}/item-definitions/{definitionId}", (HttpContext h, Guid seasonId, Guid definitionId, DefinitionWrite b, CatalogService s, CancellationToken ct) => s.DefinitionCommandAsync(h, seasonId, definitionId, b, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/item-definitions/{definitionId}/retirement", (HttpContext h, Guid seasonId, Guid definitionId, CatalogService s, CancellationToken ct) => s.DefinitionCommandAsync(h, seasonId, definitionId, null, ct));
    }
}
