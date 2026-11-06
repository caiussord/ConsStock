using ConsStock.Domain;
using ConsStock.Domain.Enums;

namespace ConsStock.UnitTests;

public sealed class StockRulesTests
{
    [Theory]
    [InlineData(MovementType.Entry, 4, 3, 7)]
    [InlineData(MovementType.Exit, 4, 3, 1)]
    [InlineData(MovementType.Adjustment, 4, 0, 0)]
    public void Calculates_new_balance(MovementType type, int current, int quantity, int expected)
        => Assert.Equal(expected, StockRules.NewBalance(type, current, quantity));

    [Fact]
    public void Rejects_negative_balance()
        => Assert.Throws<InvalidOperationException>(() => StockRules.NewBalance(MovementType.Exit, 2, 3));

    [Theory]
    [InlineData(MovementType.Entry, 0)]
    [InlineData(MovementType.Exit, -1)]
    [InlineData(MovementType.Adjustment, -1)]
    public void Rejects_invalid_quantities(MovementType type, int quantity)
        => Assert.Throws<ArgumentOutOfRangeException>(() => StockRules.NewBalance(type, 3, quantity));
}
