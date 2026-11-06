using Microsoft.EntityFrameworkCore;

namespace ConsStock.Infrastructure.Persistence;

public sealed class ConsStockDbContext(DbContextOptions<ConsStockDbContext> options) : DbContext(options);
