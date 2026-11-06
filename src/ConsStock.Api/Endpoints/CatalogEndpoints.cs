using ConsStock.Application.Stock;
using ConsStock.Domain.Entities;
using ConsStock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ConsStock.Api.Endpoints;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var categories = app.MapGroup("/api/v1/categories").WithTags("Categories");
        categories.MapGet("/", ListCategories);
        categories.MapGet("/{id:guid}", GetCategory);
        categories.MapPost("/", CreateCategory);

        var products = app.MapGroup("/api/v1/products").WithTags("Products");
        products.MapGet("/", ListProducts);
        products.MapGet("/{id:guid}", GetProduct);
        products.MapPost("/", CreateProduct);
        products.MapPut("/{id:guid}", UpdateProduct);
        return app;
    }

    private static async Task<IResult> ListCategories(ConsStockDbContext db, int page = 1,
        int pageSize = 20, bool? active = null, string? search = null, CancellationToken ct = default)
    {
        if (!Paging.Valid(page, pageSize)) return Paging.Invalid();
        var query = db.Categories.AsNoTracking().AsQueryable();
        if (active is not null) query = query.Where(x => x.IsActive == active);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => EF.Functions.ILike(x.Name, $"%{search.Trim()}%"));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.Name, x.Description, x.IsActive, x.CreatedAt }).ToListAsync(ct);
        return Results.Ok(new { items, page, pageSize, total });
    }

    private static async Task<IResult> CreateCategory(CategoryInput input, ConsStockDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 120 || input.Description?.Length > 500)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Nome obrigatório (máximo 120 caracteres); descrição até 500."] });
        var category = new Category { Name = input.Name.Trim(), Description = input.Description?.Trim() };
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/categories/{category.Id}", new
        {
            category.Id, category.Name, category.Description, category.IsActive, category.CreatedAt
        });
    }

    private static async Task<IResult> GetCategory(Guid id, ConsStockDbContext db, CancellationToken ct)
    {
        var category = await db.Categories.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Id, x.Name, x.Description, x.IsActive, x.CreatedAt })
            .SingleOrDefaultAsync(ct);
        return category is null ? Results.NotFound() : Results.Ok(category);
    }

    private static async Task<IResult> ListProducts(ConsStockDbContext db, int page = 1,
        int pageSize = 20, Guid? categoryId = null, bool? active = null, string? search = null,
        string sort = "name", CancellationToken ct = default)
    {
        if (!Paging.Valid(page, pageSize)) return Paging.Invalid();
        if (sort is not ("name" or "sku" or "createdAt"))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["sort"] = ["Use name, sku ou createdAt."] });
        var query = db.Products.AsNoTracking().AsQueryable();
        if (categoryId is not null) query = query.Where(x => x.CategoryId == categoryId);
        if (active is not null) query = query.Where(x => x.IsActive == active);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => EF.Functions.ILike(x.Name, $"%{search.Trim()}%") || EF.Functions.ILike(x.Sku, $"%{search.Trim()}%"));
        var total = await query.CountAsync(ct);
        query = sort switch
        {
            "sku" => query.OrderBy(x => x.Sku).ThenBy(x => x.Id),
            "createdAt" => query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => query.OrderBy(x => x.Name).ThenBy(x => x.Id)
        };
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.Sku, x.Name, x.CategoryId, x.ReferencePrice, x.MinimumStock, x.IsActive, x.CreatedAt })
            .ToListAsync(ct);
        return Results.Ok(new { items, page, pageSize, total });
    }

    private static async Task<IResult> GetProduct(Guid id, ConsStockDbContext db, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Id, x.Sku, x.Name, x.Description, x.CategoryId, x.ReferencePrice,
                x.MinimumStock, x.IsActive, x.CreatedAt, x.UpdatedAt, Balance = x.Balance.Quantity })
            .SingleOrDefaultAsync(ct);
        return product is null ? Results.NotFound() : Results.Ok(product);
    }

    private static async Task<IResult> CreateProduct(ProductInput input, ConsStockDbContext db, CancellationToken ct)
    {
        var validation = ValidateProduct(input);
        if (validation is not null) return validation;
        if (!await db.Categories.AnyAsync(x => x.Id == input.CategoryId && x.IsActive, ct))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["categoryId"] = ["Categoria ativa não encontrada."] });
        var product = new Product
        {
            Sku = input.Sku.Trim().ToUpperInvariant(), Name = input.Name.Trim(),
            Description = input.Description?.Trim(), CategoryId = input.CategoryId,
            ReferencePrice = input.ReferencePrice, MinimumStock = input.MinimumStock
        };
        db.Products.Add(product);
        db.StockBalances.Add(new StockBalance { ProductId = product.Id, Quantity = 0, Version = 0 });
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/products/{product.Id}", new { product.Id, product.Sku, product.Name });
    }

    private static async Task<IResult> UpdateProduct(Guid id, ProductInput input, ConsStockDbContext db, CancellationToken ct)
    {
        var validation = ValidateProduct(input);
        if (validation is not null) return validation;
        var product = await db.Products.FindAsync([id], ct);
        if (product is null) return Results.NotFound();
        if (!await db.Categories.AnyAsync(x => x.Id == input.CategoryId && x.IsActive, ct))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["categoryId"] = ["Categoria ativa não encontrada."] });
        product.Sku = input.Sku.Trim().ToUpperInvariant();
        product.Name = input.Name.Trim();
        product.Description = input.Description?.Trim();
        product.CategoryId = input.CategoryId;
        product.ReferencePrice = input.ReferencePrice;
        product.MinimumStock = input.MinimumStock;
        product.IsActive = input.IsActive;
        product.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { product.Id, product.Sku, product.Name, product.IsActive, product.UpdatedAt });
    }

    private static IResult? ValidateProduct(ProductInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Sku) || input.Sku.Length > 64 ||
            string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 160 ||
            input.Description?.Length > 1000 || input.MinimumStock < 0 ||
            input.ReferencePrice < 0 || input.ReferencePrice >= 10000000000m ||
            input.CategoryId == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["product"] = ["Confira SKU, nome, categoria, preço e estoque mínimo."] });
        return null;
    }
}

public sealed record CategoryInput(string Name, string? Description);
public sealed record ProductInput(string Sku, string Name, string? Description,
    Guid CategoryId, decimal? ReferencePrice, int MinimumStock, bool IsActive = true);

internal static class Paging
{
    public static bool Valid(int page, int pageSize) => page >= 1 && pageSize is >= 1 and <= 100 &&
        (long)(page - 1) * pageSize <= int.MaxValue;
    public static IResult Invalid() => Results.ValidationProblem(new Dictionary<string, string[]>
    {
        ["pagination"] = ["page deve ser >= 1 e pageSize entre 1 e 100."]
    });
}
