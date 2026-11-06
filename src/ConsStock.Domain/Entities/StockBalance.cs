namespace ConsStock.Domain.Entities;

public sealed class StockBalance
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public long Version { get; set; }
}
