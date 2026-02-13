using System.Security.Claims;
using Ready.Api.Auth;
using Ready.Application.Abstractions;

namespace Ready.Api.Endpoints;

public static class StatusEndpoints
{
    public static void MapStatusEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/results/{documentId}", async (
            Guid documentId,
            string type,
            string? version,
            ClaimsPrincipal user,
            IStatusReadStore store,
            CancellationToken ct) =>
        {
            var customerId = user.GetCustomerId();
            if (string.IsNullOrEmpty(customerId)) return Results.Unauthorized();

            var result = await store.GetResultAsync(documentId, customerId, type, version, ct);
            if (result is null) return Results.NotFound(new { message = "Result not found" });

            return Results.Ok(new
            {
                result.DocumentId,
                result.ResultType,
                result.Version,
                createdAt = result.CreatedAt,
                payload = result.Payload
            });
        }).RequireAuthorization();

        app.MapGet("/download/{documentId}", async (
            Guid documentId,
            string type,
            string? version,
            ClaimsPrincipal user,
            IStatusReadStore store,
            CancellationToken ct) =>
        {
            var customerId = user.GetCustomerId();
            if (string.IsNullOrEmpty(customerId)) return Results.Unauthorized();

            var file = await store.GetDownloadAsync(documentId, customerId, type, version, ct);
            if (file is null) return Results.NotFound();

            return Results.File(file.Content, file.ContentType, file.FileName);
        }).RequireAuthorization();

        app.MapGet("/status/{documentId}", async (
            Guid documentId,
            ClaimsPrincipal user,
            IStatusReadStore store,
            CancellationToken ct) =>
        {
            var customerId = user.GetCustomerId();
            if (string.IsNullOrEmpty(customerId)) return Results.Unauthorized();

            var status = await store.GetStatusAsync(documentId, customerId, ct);
            if (status is null) return Results.NotFound();

            return Results.Ok(status);
        }).RequireAuthorization();
    }
}
