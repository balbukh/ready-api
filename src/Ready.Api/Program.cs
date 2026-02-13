using Ready.Application;
using Ready.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Ready.Api.Auth;
using Ready.Infrastructure.Persistence;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

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

app.MapPost("/ingest/{workflowName}", async (
    string workflowName,
    HttpRequest request,
    ClaimsPrincipal user,
    Ready.Application.Abstractions.IFileStorage storage,
    Ready.Application.Abstractions.IDocumentStore documents,
    Ready.Application.Abstractions.IJobQueue queue,
    CancellationToken ct) =>
{
    var customerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(customerId)) return Results.Unauthorized();

    if (!request.HasFormContentType)
        return Results.BadRequest("Expected multipart/form-data");

    var form = await request.ReadFormAsync(ct);
    var file = form.Files.FirstOrDefault();
    if (file is null)
        return Results.BadRequest("No file uploaded");

    await using var stream = file.OpenReadStream();
    var (path, sha, size) = await storage.SaveAsync(stream, file.FileName, ct);

    var (docId, isNew) = await documents.CreateAsync(new(
        CustomerId: customerId,
        Source: "api",
        FileName: file.FileName,
        ContentType: file.ContentType ?? "application/octet-stream",
        SizeBytes: size,
        StoragePath: path,
        Sha256: sha
    ), ct);

    if (isNew)
    {
        await queue.EnqueueAsync(new(docId, workflowName, "v1"), ct);
    }

    return Results.Ok(new { documentId = docId, isNew });
}).RequireAuthorization();

app.MapGet("/documents", async (
    ClaimsPrincipal user,
    Ready.Application.Abstractions.IDocumentStore documents,
    CancellationToken ct) =>
{
    var customerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(customerId)) return Results.Unauthorized();

    var list = await documents.ListAsync(customerId, ct);
    return Results.Ok(list);
}).RequireAuthorization();

app.MapGet("/documents/{id}", async (
    Guid id,
    ClaimsPrincipal user,
    Ready.Application.Abstractions.IDocumentStore documents,
    Ready.Application.Abstractions.IRunStore runs,
    Ready.Application.Results.IResultStore results,
    CancellationToken ct) =>
{
    var customerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(customerId)) return Results.Unauthorized();

    var doc = await documents.GetAsync(id, ct);
    if (doc is null) return Results.NotFound();
    
    // Security check: ensure doc belongs to customer
    if (doc.CustomerId != customerId) return Results.NotFound(); 

    var runList = await runs.GetRunsAsync(id, ct);
    var latestResults = new List<Ready.Application.DTOs.WorkflowResultDto>();

    if (runList.Count > 0)
    {
        var latestRun = runList.MaxBy(r => r.StartedAt);
        if (latestRun != null)
        {
            var res = await results.GetResultsAsync(latestRun.Id, ct);
            latestResults.AddRange(res);
        }
    }

    return Results.Ok(new Ready.Application.DTOs.DocumentDetailDto(
        doc,
        runList.ToList(),
        latestResults
    ));
}).RequireAuthorization();

app.Run();

public partial class Program { }