using Microsoft.EntityFrameworkCore;
using Ready.Application.Abstractions;
using Ready.Infrastructure.Persistence;

namespace Ready.Infrastructure.Persistence;

public sealed class JobQueue : IJobQueue
{
    private readonly ReadyDbContext _db;

    public JobQueue(ReadyDbContext db) => _db = db;

    public async Task EnqueueAsync(EnqueueJobRequest request, CancellationToken ct)
    {
        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            DocumentId = request.DocumentId,
            WorkflowName = request.WorkflowName,
            WorkflowVersion = request.WorkflowVersion,
            Status = 0, // Pending
            Attempts = 0,
            NextRunAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            ParamsJson = request.Params != null && request.Params.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(request.Params)
                : null
        };

        _db.Jobs.Add(job);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<JobLease?> TryDequeueAsync(TimeSpan leaseTime, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var nextRun = now.Add(leaseTime);

        // Atomic dequeue using Postgres specific syntax
        var jobs = await _db.Jobs.FromSqlInterpolated($@"
            UPDATE ""jobs""
            SET ""Status"" = 1, ""Attempts"" = ""Attempts"" + 1, ""NextRunAt"" = {nextRun}
            WHERE ""Id"" = (
                SELECT ""Id""
                FROM ""jobs""
                WHERE ""Status"" = 0 AND ""NextRunAt"" <= {now}
                ORDER BY ""NextRunAt""
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            )
            RETURNING *").ToListAsync(ct);

        var job = jobs.FirstOrDefault();

        if (job is null)
            return null;

        Dictionary<string, string> paramsDict = [];
        if (!string.IsNullOrEmpty(job.ParamsJson))
        {
            try
            {
                paramsDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(job.ParamsJson)
                             ?? [];
            }
            catch
            {
                // ignore parsing error, return empty
            }
        }

        return new JobLease(
            job.Id,
            job.DocumentId,
            job.WorkflowName,
            job.WorkflowVersion,
            job.Attempts,
            paramsDict
        );
    }

    public async Task MarkSucceededAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _db.Jobs.FirstAsync(x => x.Id == jobId, ct);
        job.Status = 2; // Done
        job.FinishedAt = DateTimeOffset.UtcNow;
        job.LastError = null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(Guid jobId, string error, TimeSpan? retryAfter, CancellationToken ct)
    {
        var job = await _db.Jobs.FirstAsync(x => x.Id == jobId, ct);
        job.LastError = error;

        if (retryAfter is null)
        {
            job.Status = 3; // Failed
            job.FinishedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            job.Status = 0; // back to Pending
            job.NextRunAt = DateTimeOffset.UtcNow + retryAfter.Value;
        }

        await _db.SaveChangesAsync(ct);
    }
}