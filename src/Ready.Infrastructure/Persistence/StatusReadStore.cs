using Microsoft.EntityFrameworkCore;
using Ready.Application.Abstractions;
using Ready.Application.DTOs;
using Ready.Infrastructure.Persistence;

namespace Ready.Infrastructure.Persistence;

public sealed class StatusReadStore : IStatusReadStore
{
    private readonly ReadyDbContext _db;

    public StatusReadStore(ReadyDbContext db)
    {
        _db = db;
    }

    public async Task<StatusDto?> GetStatusAsync(Guid documentId, string customerId, CancellationToken ct)
    {
        // 1. Fetch Document
        var doc = await _db.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        if (doc is null || doc.CustomerId != customerId)
            return null;

        // 2. Fetch Latest Job
        var jobEntity = await _db.Jobs
            .AsNoTracking()
            .Where(j => j.DocumentId == documentId)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(ct);

        // 3. Fetch Latest Run
        var runEntity = await _db.Runs
            .AsNoTracking()
            .Where(r => r.DocumentId == documentId)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(ct);

        // 4. Fetch Details ONLY for latest run
        List<StepRunEntity> stepEntities = new();
        List<ResultEntity> resultEntities = new();

        if (runEntity is not null)
        {
            stepEntities = await _db.StepRuns
                .AsNoTracking()
                .Where(s => s.RunId == runEntity.Id)
                .OrderBy(s => s.StartedAt)
                .ToListAsync(ct);

            resultEntities = await _db.Results
                .AsNoTracking()
                .Where(r => r.RunId == runEntity.Id)
                .OrderBy(r => r.CreatedAt)
                .ToListAsync(ct);
        }

        // 5. Map to DTOs
        var docDto = new DocumentStatusDto(doc.Id, doc.CustomerId, doc.FileName, doc.CreatedAt, (int)doc.Status);
        
        var jobDto = jobEntity is null ? null : new JobStatusDto(
            jobEntity.Id, 
            jobEntity.Status, 
            jobEntity.Attempts, 
            jobEntity.NextRunAt, 
            jobEntity.FinishedAt, 
            jobEntity.LastError);

        var runDto = runEntity is null ? null : new RunStatusDto(
            runEntity.Id, 
            runEntity.Status, 
            runEntity.WorkflowName, 
            runEntity.WorkflowVersion, 
            runEntity.StartedAt, 
            runEntity.FinishedAt);

        var steps = stepEntities.Select(s => new StepStatusDto(
            s.Id, 
            s.StepName, 
            s.Status, 
            s.StartedAt, 
            s.FinishedAt)).ToList();

        var results = resultEntities.Select(r => new ResultStatusDto(
            r.Id, 
            r.ResultType, 
            r.Version, 
            r.CreatedAt)).ToList();

        return new StatusDto(docDto, jobDto, runDto, steps, results);
    }

    public async Task<ResultPayloadDto?> GetResultAsync(Guid documentId, string customerId, string type, string? version, CancellationToken ct)
    {
        // 1. Verify document ownership
        var doc = await _db.Documents
            .AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => new { d.CustomerId })
            .FirstOrDefaultAsync(ct);

        if (doc is null || doc.CustomerId != customerId)
            return null;

        // 2. Find latest run
        var latestRun = await _db.Runs
            .AsNoTracking()
            .Where(r => r.DocumentId == documentId)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (latestRun is null) return null;

        // 3. Find result
        var query = _db.Results
            .AsNoTracking()
            .Where(r => r.RunId == latestRun.Id)
            .Where(r => r.ResultType.ToLower() == type.ToLower());

        if (!string.IsNullOrEmpty(version))
            query = query.Where(r => r.Version.ToLower() == version.ToLower());

        var result = await query
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (result is null) return null;

        try
        {
            var payload = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(result.PayloadJson);
            return new ResultPayloadDto(documentId, result.ResultType, result.Version, result.CreatedAt, payload);
        }
        catch (System.Text.Json.JsonException)
        {
            // Log warning? For now just return null or throw? 
            // Better to return null as "unparseable" or let caller handle exception?
            // The requirement says "Parse payload JSON". If fails, maybe return null to indicate failure or empty.
            // Let's return null for simplicity or empty JsonElement.
            return null; 
        }
    }

    public async Task<FileDownloadDto?> GetDownloadAsync(Guid documentId, string customerId, string type, string? version, CancellationToken ct)
    {
        // Reuse logic or copy-paste query for efficiency (single query impossible due to steps)
        // 1. Verify doc
        var doc = await _db.Documents
             .AsNoTracking()
             .Where(d => d.Id == documentId)
             .Select(d => new { d.CustomerId })
             .FirstOrDefaultAsync(ct);

        if (doc is null || doc.CustomerId != customerId) return null;

        // 2. Latest run
        var latestRun = await _db.Runs
            .AsNoTracking()
            .Where(r => r.DocumentId == documentId)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (latestRun is null) return null;

        // 3. Result
        var query = _db.Results
            .AsNoTracking()
            .Where(r => r.RunId == latestRun.Id)
            .Where(r => r.ResultType.ToLower() == type.ToLower());

        if (!string.IsNullOrEmpty(version))
            query = query.Where(r => r.Version.ToLower() == version.ToLower());

        var result = await query
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (result is null) return null;

        // 4. Decode
        try
        {
            var json = System.Text.Json.JsonDocument.Parse(result.PayloadJson);
            if (json.RootElement.TryGetProperty("csvBase64", out var base64Prop))
            {
                var base64 = base64Prop.GetString();
                if (!string.IsNullOrEmpty(base64))
                {
                    var bytes = Convert.FromBase64String(base64);
                    var contentType = json.RootElement.TryGetProperty("contentType", out var ctProp) 
                        ? ctProp.GetString() 
                        : "application/octet-stream";
                    var fileName = json.RootElement.TryGetProperty("fileName", out var fnProp) 
                        ? fnProp.GetString() 
                        : $"download_{documentId}.bin";

                    return new FileDownloadDto(bytes, contentType ?? "text/csv", fileName);
                }
            }
        }
        catch
        {
            // ignore
        }
        return null;
    }
}
