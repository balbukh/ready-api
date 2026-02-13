using Microsoft.EntityFrameworkCore;
using Ready.Application.Abstractions;
using Ready.Application.DTOs;

namespace Ready.Infrastructure.Persistence;

public sealed class RunStore : IRunStore
{
    private readonly ReadyDbContext _db;
    public RunStore(ReadyDbContext db) => _db = db;

    public async Task<Guid> CreateRunAsync(Guid documentId, string workflowName, string workflowVersion, CancellationToken ct)
    {
        var run = new RunEntity
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            WorkflowName = workflowName,
            WorkflowVersion = workflowVersion,
            Status = 1, // Running
            StartedAt = DateTimeOffset.UtcNow
        };
        _db.Runs.Add(run);
        await _db.SaveChangesAsync(ct);
        return run.Id;
    }

    public async Task MarkRunSucceededAsync(Guid runId, CancellationToken ct)
    {
        var run = await _db.Runs.FindAsync([runId], ct) ?? throw new InvalidOperationException("Run not found");
        run.Status = 2;
        run.FinishedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkRunFailedAsync(Guid runId, string error, CancellationToken ct)
    {
        var run = await _db.Runs.FindAsync([runId], ct) ?? throw new InvalidOperationException("Run not found");
        run.Status = 3;
        run.FinishedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Guid> CreateStepRunAsync(Guid runId, string stepName, CancellationToken ct)
    {
        var step = new StepRunEntity
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            StepName = stepName,
            Status = 1, // Running
            StartedAt = DateTimeOffset.UtcNow
        };
        _db.StepRuns.Add(step);
        await _db.SaveChangesAsync(ct);
        return step.Id;
    }

    public async Task MarkStepSucceededAsync(Guid stepRunId, CancellationToken ct)
    {
        var step = await _db.StepRuns.FindAsync([stepRunId], ct) ?? throw new InvalidOperationException("StepRun not found");
        step.Status = 2;
        step.FinishedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkStepFailedAsync(Guid stepRunId, string diagnosticsJson, CancellationToken ct)
    {
        var step = await _db.StepRuns.FindAsync([stepRunId], ct) ?? throw new InvalidOperationException("StepRun not found");
        step.Status = 3;
        step.DiagnosticsJson = diagnosticsJson;
        step.FinishedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowRunDto>> GetRunsAsync(Guid documentId, CancellationToken ct)
    {
        return await _db.Runs
            .Where(x => x.DocumentId == documentId)
            .OrderByDescending(x => x.StartedAt)
            .Select(x => new WorkflowRunDto(
                x.Id,
                x.WorkflowName,
                x.WorkflowVersion,
                x.Status,
                x.StartedAt,
                x.FinishedAt
            ))
            .ToListAsync(ct);
    }
}