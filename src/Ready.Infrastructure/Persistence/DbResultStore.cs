using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ready.Application.Results;

namespace Ready.Infrastructure.Persistence;

public sealed class DbResultStore : IResultStore
{
    private readonly ReadyDbContext _db;

    public DbResultStore(ReadyDbContext db) => _db = db;

    public async Task SaveAsync(Guid runId, string resultType, string version, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload);

        _db.Results.Add(new ResultEntity
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            ResultType = resultType,
            Version = version,
            PayloadJson = json,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Ready.Application.DTOs.WorkflowResultDto>> GetResultsAsync(Guid runId, CancellationToken ct)
    {
        // Must materialize first because PayloadJson -> object needs deserialization
        var entities = await _db.Results
            .Where(x => x.RunId == runId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        var dtos = new List<Ready.Application.DTOs.WorkflowResultDto>();
        foreach (var e in entities)
        {
            object payload = new object();
            try
            {
                payload = JsonSerializer.Deserialize<object>(e.PayloadJson) ?? new object();
            }
            catch
            {
                payload = new { error = "Failed to deserialize" };
            }

            dtos.Add(new Ready.Application.DTOs.WorkflowResultDto(
                e.ResultType,
                e.Version,
                payload,
                e.CreatedAt
            ));
        }

        return dtos;
    }
}