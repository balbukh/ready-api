using System.Security.Cryptography;
using Ready.Application.Abstractions;

namespace Ready.Infrastructure.Storage;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(string root) => _root = root;

    public async Task<(string StoragePath, string Sha256, long SizeBytes)> SaveAsync(
        Stream content,
        string fileName,
        CancellationToken ct)
    {
        Directory.CreateDirectory(_root);

        var safeName = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
        var fullPath = Path.Combine(_root, safeName);

        using var sha = SHA256.Create();
        await using var fs = File.Create(fullPath);

        // hash while writing
        var buffer = new byte[81920];
        int read;
        long total = 0;
        while ((read = await content.ReadAsync(buffer, ct)) > 0)
        {
            await fs.WriteAsync(buffer.AsMemory(0, read), ct);
            sha.TransformBlock(buffer, 0, read, null, 0);
            total += read;
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

        var hashHex = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        return (fullPath, hashHex, total);
    }

    public Task<Stream> GetAsync(string storagePath, CancellationToken ct)
    {
        if (!File.Exists(storagePath))
        {
            throw new FileNotFoundException("File not found in storage", storagePath);
        }

        // Open with FileShare.Read to allow concurrent reads
        var fs = new FileStream(storagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return Task.FromResult<Stream>(fs);
    }
}