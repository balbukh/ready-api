using Ready.Application.Abstractions;
using Ready.Infrastructure.Storage;
using Xunit;

namespace Ready.UnitTests;

public class FileStorageTests
{
    [Fact]
    public async Task LocalFileStorage_ShouldSaveAndRetrieveFile()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "ReadyTests_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            IFileStorage storage = new LocalFileStorage(tempDir);
            var content = new byte[] { 1, 2, 3, 4, 5 };
            using var ms = new MemoryStream(content);

            // Act - Save
            var (path, sha, size) = await storage.SaveAsync(ms, "test.bin", CancellationToken.None);

            // Assert - Save
            Assert.True(File.Exists(path));
            Assert.Equal(content.Length, size);
            Assert.False(string.IsNullOrWhiteSpace(sha));

            // Act - Get
            using var retrievedStream = await storage.GetAsync(path, CancellationToken.None);
            using var msRetrieved = new MemoryStream();
            await retrievedStream.CopyToAsync(msRetrieved);

            // Assert - Get
            Assert.Equal(content, msRetrieved.ToArray());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
