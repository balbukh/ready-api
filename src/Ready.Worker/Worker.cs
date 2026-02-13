using Ready.Application.Abstractions;
using Ready.Application.Workflows;

namespace Ready.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started");

        var lease = TimeSpan.FromMinutes(2);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var queue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
                var executor = scope.ServiceProvider.GetRequiredService<WorkflowExecutor>();
                var db = scope.ServiceProvider.GetRequiredService<Ready.Infrastructure.Persistence.ReadyDbContext>();

                var job = await queue.TryDequeueAsync(lease, stoppingToken);

                if (job is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    continue;
                }

                _logger.LogInformation("Dequeued job {JobId} doc={DocId} wf={Wf} v={Ver} attempts={Attempts}",
                    job.JobId, job.DocumentId, job.WorkflowName, job.WorkflowVersion, job.Attempts);

                var doc = await db.Documents.FindAsync(new object[] { job.DocumentId }, stoppingToken);
                if (doc is null)
                {
                    _logger.LogError("Document {DocumentId} not found for job {JobId}", job.DocumentId, job.JobId);
                    await queue.MarkFailedAsync(job.JobId, "Document not found", null, stoppingToken);
                    continue;
                }

                await executor.ExecuteAsync(job.DocumentId, doc.CustomerId, job.WorkflowName, job.WorkflowVersion, job.Params, stoppingToken);

                await queue.MarkSucceededAsync(job.JobId, stoppingToken);
                _logger.LogInformation("Job succeeded {JobId}", job.JobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker loop error");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }
}