using ConsStock.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ConsStock.Infrastructure.Persistence;

public class ConsStockDbContext(DbContextOptions<ConsStockDbContext> options)
    : IdentityDbContext<IdentityUser<Guid>, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
        });
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Sku).IsUnique();
            entity.Property(x => x.Sku).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.ReferencePrice).HasPrecision(12, 2);
            entity.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<StockBalance>(entity =>
        {
            entity.HasKey(x => x.ProductId);
            entity.HasOne(x => x.Product).WithOne(x => x.Balance).HasForeignKey<StockBalance>(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Quantity).IsRequired();
            entity.Property(x => x.Version).IsRequired();
            entity.ToTable(t => t.HasCheckConstraint("CK_StockBalance_Quantity", "\"Quantity\" >= 0"));
        });
        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProductId, x.CreatedAt });
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CreatedByUserId).HasMaxLength(120).IsRequired();
        });
    }
}
