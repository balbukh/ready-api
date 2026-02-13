namespace Ready.Infrastructure.Persistence;

public sealed class DocumentEntity
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = default!;
    public string Source { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }
    public string StoragePath { get; set; } = default!;
    public string Sha256 { get; set; } = default!;
    public int Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}