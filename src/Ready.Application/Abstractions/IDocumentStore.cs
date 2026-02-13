using Ready.Application.DTOs;

namespace Ready.Application.Abstractions;

public sealed record CreateDocumentRequest(
    string CustomerId,
    string Source,
    string FileName,
    string ContentType,
    long SizeBytes,
    string StoragePath,
    string Sha256
);

public interface IDocumentStore
{
    Task<(Guid Id, bool IsNew)> CreateAsync(CreateDocumentRequest request, CancellationToken ct);
    Task<DocumentDto?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DocumentDto>> ListAsync(string customerId, CancellationToken ct);
}