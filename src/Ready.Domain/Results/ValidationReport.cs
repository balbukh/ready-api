namespace Ready.Domain.Results;

public sealed record ValidationReport(
    bool IsValid,
    IReadOnlyList<string> Issues
)
{
    public const string ResultType = "ValidationReport";
    public const string Version = "v1";
}
