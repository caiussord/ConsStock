using ConsStock.Domain.Entities;
using ConsStock.Domain.Enums;

namespace ConsStock.Application.Stock;

public sealed record CreateMovement(Guid ProductId, MovementType Type, int Quantity, string Reason, string CreatedByUserId);

public interface IStockMovementService
{
    Task<StockMovement> CreateAsync(CreateMovement request, CancellationToken cancellationToken);
}

public sealed class StockConflictException(string message) : Exception(message);
