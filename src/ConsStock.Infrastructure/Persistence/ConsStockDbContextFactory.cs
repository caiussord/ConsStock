using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ConsStock.Infrastructure.Persistence;

public sealed class ConsStockDbContextFactory : IDesignTimeDbContextFactory<ConsStockDbContext>
{
    public ConsStockDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=consstock;Username=consstock;Password=placeholder";
        var options = new DbContextOptionsBuilder<ConsStockDbContext>()
            .UseNpgsql(connectionString).Options;
        return new ConsStockDbContext(options);
    }
}
