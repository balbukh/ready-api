using System.Security.Claims;
using Ready.Api.Auth;
using Ready.Application.Abstractions;

namespace Ready.Api.Endpoints;

public static class IngestEndpoints
{
    public static void MapIngestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/ingest/{workflowName}", async (
            string workflowName,
            HttpRequest request,
            ClaimsPrincipal user,
            IFileStorage storage,
            IDocumentStore documents,
            IJobQueue queue,
            CancellationToken ct) =>
        {
            var customerId = user.GetCustomerId();
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
    }
}
