using Ready.Application.DTOs;

namespace Ready.Application.Abstractions;

public interface IStatusReadStore
{
    Task<StatusDto?> GetStatusAsync(Guid documentId, string customerId, CancellationToken ct);
    Task<ResultPayloadDto?> GetResultAsync(Guid documentId, string customerId, string type, string? version, CancellationToken ct);
    Task<FileDownloadDto?> GetDownloadAsync(Guid documentId, string customerId, string type, string? version, CancellationToken ct);
}
