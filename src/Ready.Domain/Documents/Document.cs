namespace Ready.Domain.Documents;

public sealed class Document
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string CustomerId { get; init; } = default!;
    public string Source { get; init; } = default!; // "api" | "telegram"
    public string FileName { get; init; } = default!;
    public string ContentType { get; init; } = default!;
    public long SizeBytes { get; init; }
    public string StoragePath { get; set; } = default!;
    public string Sha256 { get; set; } = default!;
    public DocumentStatus Status { get; set; } = DocumentStatus.Received;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}