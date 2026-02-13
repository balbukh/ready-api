using Ready.Application;
using Ready.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Ready.Api.Auth;
using Ready.Infrastructure.Persistence;
using Ready.Api.Endpoints;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReadyApplication(builder.Configuration);

var cs = builder.Configuration.GetConnectionString("ReadyDb")!;
builder.Services.AddReadyInfrastructure(cs);

// Add Auth services
builder.Services.AddAuthentication("ApiKey")
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>("ApiKey", null);
builder.Services.AddAuthorization();

var app = builder.Build();

// Seeding for Demo
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReadyDbContext>();
    // Run migrations at startup
    await db.Database.MigrateAsync();

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
        await db.SaveChangesAsync();
    }
}

app.UseAuthentication();
app.UseAuthorization();

// Map Endpoints
app.MapIngestEndpoints();
app.MapDocumentEndpoints();
app.MapStatusEndpoints();

app.Run();

public partial class Program { }