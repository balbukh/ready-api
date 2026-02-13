namespace Ready.Application.Workflows;

public sealed record WorkflowDefinition(
    string Name,
    string Version,
    IReadOnlyList<string> Steps
);