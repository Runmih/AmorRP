using AmorRP.Contracts.Inventory;
namespace AmorRP.Server.Features.Catalog;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/groups/{groupId}/categories", (HttpContext h, Guid groupId, CatalogService s, CancellationToken ct) => s.CategoriesAsync(h, groupId, null, ct));
        app.MapGet("/api/v1/groups/{groupId}/categories/{categoryId}", (HttpContext h, Guid groupId, Guid categoryId, CatalogService s, CancellationToken ct) => s.CategoriesAsync(h, groupId, categoryId, ct));
        app.MapPost("/api/v1/groups/{groupId}/categories", (HttpContext h, Guid groupId, CategoryEdit b, CatalogService s, CancellationToken ct) => s.CategoryCommandAsync(h, groupId, null, b, ct));
        app.MapPatch("/api/v1/groups/{groupId}/categories/{categoryId}", (HttpContext h, Guid groupId, Guid categoryId, CategoryEdit b, CatalogService s, CancellationToken ct) => s.CategoryCommandAsync(h, groupId, categoryId, b, ct));
        app.MapPost("/api/v1/groups/{groupId}/categories/{categoryId}/retirement", (HttpContext h, Guid groupId, Guid categoryId, CatalogService s, CancellationToken ct) => s.CategoryCommandAsync(h, groupId, categoryId, null, ct));
        app.MapGet("/api/v1/groups/{groupId}/item-definitions", (HttpContext h, Guid groupId, CatalogService s, CancellationToken ct) => s.DefinitionsAsync(h, groupId, null, null, ct));
        app.MapGet("/api/v1/groups/{groupId}/item-definitions/{definitionId}", (HttpContext h, Guid groupId, Guid definitionId, int? revision, CatalogService s, CancellationToken ct) => s.DefinitionsAsync(h, groupId, definitionId, revision, ct));
        app.MapPost("/api/v1/groups/{groupId}/item-definitions", (HttpContext h, Guid groupId, DefinitionWrite b, CatalogService s, CancellationToken ct) => s.DefinitionCommandAsync(h, groupId, null, b, ct));
        app.MapPatch("/api/v1/groups/{groupId}/item-definitions/{definitionId}", (HttpContext h, Guid groupId, Guid definitionId, DefinitionWrite b, CatalogService s, CancellationToken ct) => s.DefinitionCommandAsync(h, groupId, definitionId, b, ct));
        app.MapPost("/api/v1/groups/{groupId}/item-definitions/{definitionId}/retirement", (HttpContext h, Guid groupId, Guid definitionId, CatalogService s, CancellationToken ct) => s.DefinitionCommandAsync(h, groupId, definitionId, null, ct));
    }
}
