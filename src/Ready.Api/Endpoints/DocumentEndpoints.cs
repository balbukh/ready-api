using System.Security.Claims;
using Ready.Api.Auth;
using Ready.Application.Abstractions;
using Ready.Application.Results;
using Ready.Application.DTOs;

namespace Ready.Api.Endpoints;

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/documents", async (
            ClaimsPrincipal user,
            IDocumentStore documents,
            CancellationToken ct) =>
        {
            var customerId = user.GetCustomerId();
            if (string.IsNullOrEmpty(customerId)) return Results.Unauthorized();

            var list = await documents.ListAsync(customerId, ct);
            return Results.Ok(list);
        }).RequireAuthorization();

        app.MapGet("/documents/{id}", async (
            Guid id,
            ClaimsPrincipal user,
            IDocumentStore documents,
            IRunStore runs,
            IResultStore results,
            CancellationToken ct) =>
        {
            var customerId = user.GetCustomerId();
            if (string.IsNullOrEmpty(customerId)) return Results.Unauthorized();

            var doc = await documents.GetAsync(id, ct);
            if (doc is null) return Results.NotFound();
            
            // Security check: ensure doc belongs to customer
            if (doc.CustomerId != customerId) return Results.NotFound(); 

            var runList = await runs.GetRunsAsync(id, ct);
            var latestResults = new List<WorkflowResultDto>();

            if (runList.Count > 0)
            {
                var latestRun = runList.MaxBy(r => r.StartedAt);
                if (latestRun != null)
                {
                    var res = await results.GetResultsAsync(latestRun.Id, ct);
                    latestResults.AddRange(res);
                }
            }

            return Results.Ok(new DocumentDetailDto(
                doc,
                runList.ToList(),
                latestResults
            ));
        }).RequireAuthorization();
    }
}
