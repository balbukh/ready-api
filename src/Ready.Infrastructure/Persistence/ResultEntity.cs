namespace Ready.Infrastructure.Persistence;

public sealed class ResultEntity
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string ResultType { get; set; } = default!;
    public string Version { get; set; } = default!;
    public string PayloadJson { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}