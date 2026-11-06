using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConsStock.Application.Stock;
using ConsStock.Domain.Entities;
using ConsStock.Domain.Enums;
using ConsStock.Infrastructure.Persistence;
using ConsStock.Infrastructure.Stock;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ConsStock.IntegrationTests;

public sealed class InventoryFlowTests
{
    [Fact]
    public async Task Failed_history_insert_rolls_back_balance_update()
    {
        var connection = Environment.GetEnvironmentVariable("TEST_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Configure TEST_DATABASE_CONNECTION para os testes de integração.");
        var options = new DbContextOptionsBuilder<ConsStockDbContext>().UseNpgsql(connection).Options;
        var category = new Category { Name = "Rollback " + Guid.NewGuid().ToString("N") };
        var product = new Product
        {
            Sku = "ROLLBACK-" + Guid.NewGuid().ToString("N"), Name = "Rollback product",
            CategoryId = category.Id
        };
        await using (var setup = new ConsStockDbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.Categories.Add(category);
            setup.Products.Add(product);
            setup.StockBalances.Add(new StockBalance { ProductId = product.Id, Quantity = 10 });
            await setup.SaveChangesAsync();
        }

        await using (var failing = new FailingSaveDbContext(options))
        {
            var service = new StockMovementService(failing);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
                new CreateMovement(product.Id, MovementType.Exit, 4, "Forced failure", "tester"),
                CancellationToken.None));
        }

        await using var verify = new ConsStockDbContext(options);
        Assert.Equal(10, await verify.StockBalances.Where(x => x.ProductId == product.Id)
            .Select(x => x.Quantity).SingleAsync());
        Assert.Equal(0, await verify.StockMovements.CountAsync(x => x.ProductId == product.Id));
    }

    [Fact]
    public async Task Inventory_flow_preserves_balance_and_history_under_concurrent_exits()
    {
        var connection = Environment.GetEnvironmentVariable("TEST_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Configure TEST_DATABASE_CONNECTION para os testes de integração.");
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connection,
                    ["Database:ApplyMigrations"] = "true"
                });
            }));
        using var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");

        var categoryResponse = await client.PostAsJsonAsync("/api/v1/categories", new
        {
            name = "Test " + suffix, description = "Integration test"
        });
        Assert.Equal(HttpStatusCode.Created, categoryResponse.StatusCode);
        var categoryId = (await Read(categoryResponse)).GetProperty("id").GetGuid();

        var productInput = new
        {
            sku = "TEST-" + suffix, name = "Test product", description = "Test",
            categoryId, referencePrice = 12.50m, minimumStock = 5, isActive = true
        };
        var productResponse = await client.PostAsJsonAsync("/api/v1/products", productInput);
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        var productId = (await Read(productResponse)).GetProperty("id").GetGuid();
        var duplicate = await client.PostAsJsonAsync("/api/v1/products", productInput);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("application/problem+json", duplicate.Content.Headers.ContentType?.MediaType);

        var initialLow = await Read(await client.GetAsync("/api/v1/stock/low?page=1&pageSize=100"));
        Assert.Contains(initialLow.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("productId").GetGuid() == productId);

        var invalid = await Move(client, productId, "Entry", 0);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var tooLargeExit = await Move(client, productId, "Exit", 1);
        Assert.Equal(HttpStatusCode.Conflict, tooLargeExit.StatusCode);

        var entry = await Move(client, productId, "Entry", 10);
        Assert.Equal(HttpStatusCode.Created, entry.StatusCode);
        Assert.Equal(10, (await Read(entry)).GetProperty("newBalance").GetInt32());

        var exits = await Task.WhenAll(Move(client, productId, "Exit", 7),
            Move(client, productId, "Exit", 7));
        Assert.Single(exits, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(exits, response => response.StatusCode == HttpStatusCode.Conflict);

        var detail = await Read(await client.GetAsync($"/api/v1/products/{productId}"));
        Assert.Equal(3, detail.GetProperty("balance").GetInt32());
        var history = await Read(await client.GetAsync($"/api/v1/stock/movements?productId={productId}&pageSize=1"));
        Assert.Equal(2, history.GetProperty("total").GetInt32());
        Assert.Single(history.GetProperty("items").EnumerateArray());

        var adjustment = await Move(client, productId, "Adjustment", 6);
        Assert.Equal(HttpStatusCode.Created, adjustment.StatusCode);
        var adjusted = await Read(adjustment);
        Assert.Equal(3, adjusted.GetProperty("previousBalance").GetInt32());
        Assert.Equal(6, adjusted.GetProperty("newBalance").GetInt32());
        var filtered = await Read(await client.GetAsync($"/api/v1/products?search={suffix}&pageSize=1"));
        Assert.Equal(1, filtered.GetProperty("total").GetInt32());
    }

    private static Task<HttpResponseMessage> Move(HttpClient client, Guid productId, string type, int quantity)
        => client.PostAsJsonAsync("/api/v1/stock/movements", new
        {
            productId, type, quantity, reason = "Integration test", createdByUserId = "test-operator"
        });

    private static async Task<JsonElement> Read(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private sealed class FailingSaveDbContext(DbContextOptions<ConsStockDbContext> options)
        : ConsStockDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated history failure");
    }
}
