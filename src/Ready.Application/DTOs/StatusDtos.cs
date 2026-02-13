namespace Ready.Application.DTOs;

public record StatusDto(
    DocumentStatusDto Document,
    JobStatusDto? Job,
    RunStatusDto? Run,
    List<StepStatusDto> Steps,
    List<ResultStatusDto> Results
);

public record DocumentStatusDto(Guid Id, string CustomerId, string FileName, DateTimeOffset CreatedAt, int Status);
public record JobStatusDto(Guid Id, int Status, int Attempts, DateTimeOffset NextRunAt, DateTimeOffset? FinishedAt, string? LastError);
public record RunStatusDto(Guid Id, int Status, string WorkflowName, string WorkflowVersion, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt);
public record StepStatusDto(Guid Id, string StepName, int Status, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt);
public record ResultStatusDto(Guid Id, string ResultType, string Version, DateTimeOffset CreatedAt);

public record ResultPayloadDto(Guid DocumentId, string ResultType, string Version, DateTimeOffset CreatedAt, System.Text.Json.JsonElement Payload);
public record FileDownloadDto(byte[] Content, string ContentType, string FileName);
