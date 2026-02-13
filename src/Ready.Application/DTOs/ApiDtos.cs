namespace Ready.Application.DTOs;

public record DocumentDto(
    Guid Id,
    string CustomerId,
    string FileName,
    int Status,
    DateTimeOffset CreatedAt
);

public record WorkflowRunDto(
    Guid Id,
    string WorkflowName,
    string WorkflowVersion,
    int Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt
);

public record WorkflowResultDto(
    string ResultType,
    string Version,
    object Payload,
    DateTimeOffset CreatedAt
);

public record DocumentDetailDto(
    DocumentDto Document,
    List<WorkflowRunDto> Runs,
    List<WorkflowResultDto> LatestResults
);
