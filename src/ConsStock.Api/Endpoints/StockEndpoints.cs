using System.Security.Claims;
using ConsStock.Application.Stock;
using ConsStock.Domain.Enums;
using ConsStock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConsStock.Api.Endpoints;

public static class StockEndpoints
{
    public static IEndpointRouteBuilder MapStockEndpoints(this IEndpointRouteBuilder app)
    {
        var stock = app.MapGroup("/api/v1/stock").WithTags("Stock").RequireAuthorization();
        stock.MapGet("/", ListBalances);
        stock.MapGet("/low", ListLowStock);
        stock.MapGet("/movements", ListMovements);
        stock.MapPost("/movements", CreateMovement).RequireAuthorization("StockWrite");
        return app;
    }

    private static async Task<IResult> ListBalances(ConsStockDbContext db, int page = 1,
        int pageSize = 20, Guid? categoryId = null, bool? active = null, CancellationToken ct = default)
    {
        if (!Paging.Valid(page, pageSize)) return Paging.Invalid();
        var query = db.StockBalances.AsNoTracking().AsQueryable();
        if (categoryId is not null) query = query.Where(x => x.Product.CategoryId == categoryId);
        if (active is not null) query = query.Where(x => x.Product.IsActive == active);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Product.Sku).ThenBy(x => x.ProductId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.ProductId, x.Product.Sku, x.Product.Name, x.Quantity,
                x.Product.MinimumStock, x.Product.IsActive })
            .ToListAsync(ct);
        return Results.Ok(new { items, page, pageSize, total });
    }

    private static async Task<IResult> ListLowStock(ConsStockDbContext db, int page = 1,
        int pageSize = 20, CancellationToken ct = default)
    {
        if (!Paging.Valid(page, pageSize)) return Paging.Invalid();
        var query = db.StockBalances.AsNoTracking()
            .Where(x => x.Product.IsActive && x.Quantity < x.Product.MinimumStock);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Product.Sku).ThenBy(x => x.ProductId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.ProductId, x.Product.Sku, x.Product.Name, x.Quantity, x.Product.MinimumStock })
            .ToListAsync(ct);
        return Results.Ok(new { items, page, pageSize, total });
    }

    private static async Task<IResult> ListMovements(ConsStockDbContext db, int page = 1,
        int pageSize = 20, Guid? productId = null, string? type = null, CancellationToken ct = default)
    {
        if (!Paging.Valid(page, pageSize)) return Paging.Invalid();
        var query = db.StockMovements.AsNoTracking().AsQueryable();
        if (productId is not null) query = query.Where(x => x.ProductId == productId);
        if (type is not null)
        {
            if (!Enum.TryParse<MovementType>(type, true, out var parsed) || !Enum.IsDefined(parsed))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["type"] = ["Use Entry, Exit ou Adjustment."] });
            query = query.Where(x => x.Type == parsed);
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.ProductId, x.Type, x.Quantity, x.PreviousBalance,
                x.NewBalance, x.Reason, x.CreatedByUserId, x.CreatedAt })
            .ToListAsync(ct);
        return Results.Ok(new { items, page, pageSize, total });
    }

    private static async Task<IResult> CreateMovement(StockMovementInput input,
        IStockMovementService service, ClaimsPrincipal user, CancellationToken ct)
    {
        var actor = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actor is null) return Results.Unauthorized();
        var request = new CreateMovement(input.ProductId, input.Type, input.Quantity, input.Reason, actor);
        var movement = await service.CreateAsync(request, ct);
        return Results.Created($"/api/v1/stock/movements?productId={movement.ProductId}", new
        {
            movement.Id, movement.ProductId, movement.Type, movement.Quantity,
            movement.PreviousBalance, movement.NewBalance, movement.Reason,
            movement.CreatedByUserId, movement.CreatedAt
        });
    }
}

public sealed record StockMovementInput(Guid ProductId, MovementType Type, int Quantity, string Reason);
