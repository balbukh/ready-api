using Microsoft.Extensions.Logging;
using Ready.Application.Abstractions;
using Ready.Application.Results;

namespace Ready.Application.Workflows;

public sealed class WorkflowExecutor
{
    private readonly IWorkflowRegistry _registry;
    private readonly IReadOnlyDictionary<string, IWorkflowStep> _stepMap;
    private readonly IResultStore _resultStore;
    private readonly IRunStore _runStore;
    private readonly ILogger<WorkflowExecutor> _logger;

    public WorkflowExecutor(
        IWorkflowRegistry registry,
        IEnumerable<IWorkflowStep> steps,
        IResultStore resultStore,
        IRunStore runStore,
        ILogger<WorkflowExecutor> logger)
    {
        _registry = registry;
        _resultStore = resultStore;
        _runStore = runStore;
        _logger = logger;

        _stepMap = steps.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
    }

    public async Task ExecuteAsync(
        Guid documentId,
        string customerId,
        string workflowName,
        string workflowVersion,
        Dictionary<string, string>? initialParams,
        CancellationToken ct)
    {
        var correlationId = Guid.NewGuid();

        var wf = _registry.Get(workflowName, workflowVersion);

        _logger.LogInformation(
            "Workflow start: {Workflow} {Version} doc={DocumentId} corr={CorrelationId}",
            wf.Name, wf.Version, documentId, correlationId);

        // 1) Create Run
        var runId = await _runStore.CreateRunAsync(documentId, wf.Name, wf.Version, ct);

        var ctx = new StepContext(
            documentId: documentId,
            customerId: customerId,
            workflowName: wf.Name,
            workflowVersion: wf.Version,
            correlationId: correlationId,
            runId: runId,
            initialParams: initialParams);

        try
        {
            foreach (var stepName in wf.Steps)
            {
                if (!_stepMap.TryGetValue(stepName, out var step))
                    throw new InvalidOperationException($"Step not registered: {stepName}");

                _logger.LogInformation(
                    "Step start: {Step} run={RunId} doc={DocumentId} corr={CorrelationId}",
                    stepName, runId, documentId, correlationId);

                // 2) Create StepRun
                var stepRunId = await _runStore.CreateStepRunAsync(runId, stepName, ct);

                try
                {
                    var outcome = await step.ExecuteAsync(ctx, ct);

                    _logger.LogInformation(
                        "Step end: {Step} status={Status} msg={Msg} run={RunId} doc={DocumentId} corr={CorrelationId}",
                        stepName, outcome.Status, outcome.Message, runId, documentId, correlationId);

                    // 3) Save result if any
                    if (outcome.Result is not null)
                    {
                        var (resType, ver, payload) = outcome.Result is IResultEnvelope env
                            ? (env.ResultType, env.Version, env.Payload)
                            : (outcome.Result.GetType().Name, "v1", outcome.Result);

                        await _resultStore.SaveAsync(
                            runId,
                            resType,
                            ver,
                            payload,
                            ct);
                    }

                    // 4) Handle outcome status
                    if (outcome.Status == StepOutcomeStatus.Succeeded || outcome.Status == StepOutcomeStatus.Skipped)
                    {
                        await _runStore.MarkStepSucceededAsync(stepRunId, ct);
                        continue;
                    }

                    if (outcome.Status == StepOutcomeStatus.Retry)
                    {
                        // На рівні workflow ми не ретраїмо.
                        // Ретрай робить job queue (перезапуск всього workflow).
                        await _runStore.MarkStepFailedAsync(stepRunId, outcome.Message ?? "retry requested", ct);
                        throw new InvalidOperationException($"Retry requested at step {stepName}: {outcome.Message}");
                    }

                    // Failed
                    await _runStore.MarkStepFailedAsync(stepRunId, outcome.Message ?? "failed", ct);
                    throw new InvalidOperationException($"Workflow failed at step {stepName}: {outcome.Message}");
                }
                catch (Exception ex)
                {
                    // Ensure StepRun failed is recorded
                    try
                    {
                        await _runStore.MarkStepFailedAsync(stepRunId, ex.Message, ct);
                    }
                    catch
                    {
                        // ignore secondary failure
                    }

                    throw;
                }
            }

            // 5) Mark run succeeded
            await _runStore.MarkRunSucceededAsync(runId, ct);

            _logger.LogInformation(
                "Workflow completed: {Workflow} {Version} run={RunId} doc={DocumentId} corr={CorrelationId}",
                wf.Name, wf.Version, runId, documentId, correlationId);
        }
        catch (Exception ex)
        {
            await _runStore.MarkRunFailedAsync(runId, ex.Message, ct);

            _logger.LogError(
                ex,
                "Workflow failed: {Workflow} {Version} run={RunId} doc={DocumentId} corr={CorrelationId}",
                wf.Name, wf.Version, runId, documentId, correlationId);

            throw;
        }
    }
}