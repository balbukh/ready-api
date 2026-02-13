namespace Ready.Application.Abstractions;

public sealed record StepOutcome(
    StepOutcomeStatus Status,
    string? Message = null,
    TimeSpan? RetryAfter = null,
    object? Result = null
)
{
    public static StepOutcome Succeeded(string? message = null, object? result = null) =>
        new(StepOutcomeStatus.Succeeded, message, null, result);

    public static StepOutcome Failed(string message) =>
        new(StepOutcomeStatus.Failed, message);

    public static StepOutcome Retry(string message, TimeSpan? retryAfter = null) =>
        new(StepOutcomeStatus.Retry, message, retryAfter);
}

public enum StepOutcomeStatus
{
    Succeeded = 0,
    Failed = 1,
    Retry = 2,
    Skipped = 3
}