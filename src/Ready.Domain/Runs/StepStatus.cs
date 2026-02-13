namespace Ready.Domain.Runs;

public enum StepStatus
{
    Created = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Retrying = 4,
    Skipped = 5
}