using ConsStock.Domain.Enums;

namespace ConsStock.Domain;

public static class StockRules
{
    public static int NewBalance(MovementType type, int current, int quantity)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        if (current < 0 || (type == MovementType.Adjustment ? quantity < 0 : quantity <= 0))
            throw new ArgumentOutOfRangeException(nameof(quantity));
        return type switch
        {
            MovementType.Entry => checked(current + quantity),
            MovementType.Exit when quantity <= current => current - quantity,
            MovementType.Exit => throw new InvalidOperationException("Saldo insuficiente."),
            MovementType.Adjustment => quantity,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
}
