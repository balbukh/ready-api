namespace Ready.Application.Workflows;

public interface IWorkflowRegistry
{
    WorkflowDefinition Get(string workflowName, string? version = null);
}