using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ready.Infrastructure.Persistence;
using Xunit;

namespace Ready.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing context registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ReadyDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add new context with test connection string
            // Note: In real world, use Docker/TestContainers. Here we assume local Postgres is available and we can create a side DB.
            var connString = "Host=localhost;Port=5432;Database=ready_test_auth;Username=ready;Password=ready";
            
            services.AddDbContext<ReadyDbContext>(options =>
            {
                options.UseNpgsql(connString);
            });

            // Build SP to run migrations/ensure created
            var sp = services.BuildServiceProvider();

            using (var scope = sp.CreateScope())
            {
                var scopedServices = scope.ServiceProvider;
                var db = scopedServices.GetRequiredService<ReadyDbContext>();
                
                // Reset DB
                db.Database.EnsureDeleted();
                db.Database.EnsureCreated();
                
                // Seed if needed (Program.cs seeding might run too, but let's be sure)
                if (!db.ApiKeys.Any(k => k.Key == "demo-key-123"))
                {
                    db.ApiKeys.Add(new ApiKeyEntity
                    {
                        Id = Guid.NewGuid(),
                        Key = "demo-key-123",
                        CustomerId = "demo-customer",
                        Label = "Demo Key",
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                    db.SaveChanges();
                }
            }
        });
    }
}

public class ApiKeyAuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ApiKeyAuthTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDocuments_MissingKey_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/documents");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDocuments_InvalidKey_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "invalid-key");
        var response = await client.GetAsync("/documents");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDocuments_ValidKey_ReturnsOk()
    {
        var client = _factory.CreateClient();
        
        client.DefaultRequestHeaders.Add("X-Api-Key", "demo-key-123");
        var response = await client.GetAsync("/documents");
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
