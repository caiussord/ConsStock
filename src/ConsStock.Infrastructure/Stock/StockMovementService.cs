using ConsStock.Application.Stock;
using ConsStock.Domain;
using ConsStock.Domain.Entities;
using ConsStock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConsStock.Infrastructure.Stock;

public sealed class StockMovementService(ConsStockDbContext db) : IStockMovementService
{
    public async Task<StockMovement> CreateAsync(CreateMovement request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500 ||
            string.IsNullOrWhiteSpace(request.CreatedByUserId) || request.CreatedByUserId.Length > 120)
            throw new ArgumentException("Motivo e responsável são obrigatórios e devem respeitar os limites de tamanho.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var active = await db.Products.AsNoTracking()
            .AnyAsync(x => x.Id == request.ProductId && x.IsActive, cancellationToken);
        if (!active) throw new KeyNotFoundException("Produto ativo não encontrado.");

        var balance = await db.StockBalances.AsNoTracking()
            .SingleAsync(x => x.ProductId == request.ProductId, cancellationToken);
        var newBalance = StockRules.NewBalance(request.Type, balance.Quantity, request.Quantity);
        var updated = await db.StockBalances
            .Where(x => x.ProductId == request.ProductId && x.Version == balance.Version)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Quantity, newBalance)
                .SetProperty(x => x.Version, balance.Version + 1), cancellationToken);
        if (updated != 1) throw new StockConflictException("O saldo mudou durante a operação. Tente novamente.");

        var movement = new StockMovement
        {
            ProductId = request.ProductId,
            Type = request.Type,
            Quantity = request.Quantity,
            PreviousBalance = balance.Quantity,
            NewBalance = newBalance,
            Reason = request.Reason.Trim(),
            CreatedByUserId = request.CreatedByUserId.Trim()
        };
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return movement;
    }
}
