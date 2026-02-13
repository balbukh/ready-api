namespace Ready.Application.Abstractions;

public interface IFileStorage
{
    Task<(string StoragePath, string Sha256, long SizeBytes)> SaveAsync(
        Stream content,
        string fileName,
        CancellationToken ct);

    Task<Stream> GetAsync(string storagePath, CancellationToken ct);
}