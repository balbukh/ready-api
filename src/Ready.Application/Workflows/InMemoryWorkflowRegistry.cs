namespace Ready.Application.Workflows;

public sealed class InMemoryWorkflowRegistry : IWorkflowRegistry
{
    private readonly Dictionary<string, WorkflowDefinition> _workflows;

    public InMemoryWorkflowRegistry(IEnumerable<WorkflowDefinition> workflows)
    {
        _workflows = workflows.ToDictionary(
            w => $"{w.Name}:{w.Version}",
            w => w,
            StringComparer.OrdinalIgnoreCase
        );
    }

    public WorkflowDefinition Get(string workflowName, string? version = null)
    {
        var ver = string.IsNullOrWhiteSpace(version) ? "v1" : version;
        var key = $"{workflowName}:{ver}";

        if (_workflows.TryGetValue(key, out var wf))
            return wf;

        throw new InvalidOperationException($"Workflow not found: {workflowName} {ver}");
    }
}