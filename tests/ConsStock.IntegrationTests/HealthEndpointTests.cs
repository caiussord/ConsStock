using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ConsStock.IntegrationTests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Liveness_is_available_without_a_database_connection()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=localhost;Database=consstock_tests;Username=test;Password=test",
                    ["Database:ApplyMigrations"] = "false"
                });
            }));
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.True(response.IsSuccessStatusCode);
    }
}
