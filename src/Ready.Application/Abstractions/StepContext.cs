namespace Ready.Application.Abstractions;

public sealed class StepContext
{
    public StepContext(
        Guid documentId,
        string customerId,
        string workflowName,
        string workflowVersion,
        Guid correlationId,
        Guid runId,
        Dictionary<string, string>? initialParams = null)
    {
        DocumentId = documentId;
        CustomerId = customerId;
        WorkflowName = workflowName;
        WorkflowVersion = workflowVersion;
        CorrelationId = correlationId;
        RunId = runId;

        if (initialParams != null)
        {
            foreach (var kvp in initialParams)
            {
                Items[kvp.Key] = kvp.Value;
            }
        }
    }

    public Guid DocumentId { get; }
    public Guid RunId { get; }
    public string CustomerId { get; }
    public string WorkflowName { get; }
    public string WorkflowVersion { get; }
    public Guid CorrelationId { get; }

    public Dictionary<string, object> Items { get; } = new(StringComparer.OrdinalIgnoreCase);
}