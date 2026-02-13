using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ready.Application.Abstractions;
using Ready.Infrastructure.Persistence;
using Ready.Infrastructure.Steps;
using Xunit;

namespace Ready.UnitTests;

public class PdfTextExtractStepTests
{
    private static ReadyDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ReadyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ReadyDbContext(options);
    }

    private static StepContext MakeContext(Guid documentId) =>
        new(documentId, "cust-1", "invoice", "v1", Guid.NewGuid(), Guid.NewGuid());

    // ---- Helpers ----

    private sealed class FakeFileStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> _files = new();

        public void Add(string path, byte[] content) => _files[path] = content;

        public Task<(string StoragePath, string Sha256, long SizeBytes)> SaveAsync(
            Stream content, string fileName, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<Stream> GetAsync(string storagePath, CancellationToken ct)
        {
            if (_files.TryGetValue(storagePath, out var bytes))
                return Task.FromResult<Stream>(new MemoryStream(bytes));
            throw new FileNotFoundException(storagePath);
        }
    }

    // ---- Tests ----

    [Fact]
    public async Task ExecuteAsync_DocumentNotFound_ReturnsFailed()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var storage = new FakeFileStorage();
        var step = new PdfTextExtractStep(db, storage, NullLogger<PdfTextExtractStep>.Instance);
        var ctx = MakeContext(Guid.NewGuid()); // non-existent

        // Act
        var outcome = await step.ExecuteAsync(ctx, CancellationToken.None);

        // Assert
        Assert.Equal(StepOutcomeStatus.Failed, outcome.Status);
        Assert.Contains("not found", outcome.Message);
    }

    [Fact]
    public async Task ExecuteAsync_TextFile_ExtractsText()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var docId = Guid.NewGuid();
        var storagePath = "/data/test.txt";
        var fileContent = "Hello invoice world";

        db.Documents.Add(new DocumentEntity
        {
            Id = docId,
            CustomerId = "cust-1",
            Source = "upload",
            FileName = "test.txt",
            ContentType = "text/plain",
            SizeBytes = fileContent.Length,
            StoragePath = storagePath,
            Sha256 = "abc123",
            Status = 1,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var storage = new FakeFileStorage();
        storage.Add(storagePath, Encoding.UTF8.GetBytes(fileContent));

        var step = new PdfTextExtractStep(db, storage, NullLogger<PdfTextExtractStep>.Instance);
        var ctx = MakeContext(docId);

        // Act
        var outcome = await step.ExecuteAsync(ctx, CancellationToken.None);

        // Assert
        Assert.Equal(StepOutcomeStatus.Succeeded, outcome.Status);
        Assert.NotNull(outcome.Result);

        // Check ResultEnvelope
        var envelope = outcome.Result as Ready.Application.Results.IResultEnvelope;
        Assert.NotNull(envelope);
        Assert.Equal("DocText", envelope.ResultType);
        Assert.Equal("v1", envelope.Version);

        // Check payload contains the text
        var json = System.Text.Json.JsonSerializer.Serialize(envelope.Payload);
        Assert.Contains("Hello invoice world", json);
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedContentType_ReturnsEmptyText()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var docId = Guid.NewGuid();
        var storagePath = "/data/test.bin";

        db.Documents.Add(new DocumentEntity
        {
            Id = docId,
            CustomerId = "cust-1",
            Source = "upload",
            FileName = "test.bin",
            ContentType = "application/octet-stream",
            SizeBytes = 100,
            StoragePath = storagePath,
            Sha256 = "def456",
            Status = 1,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var storage = new FakeFileStorage();
        storage.Add(storagePath, new byte[] { 0x00, 0x01, 0x02 });

        var step = new PdfTextExtractStep(db, storage, NullLogger<PdfTextExtractStep>.Instance);
        var ctx = MakeContext(docId);

        // Act
        var outcome = await step.ExecuteAsync(ctx, CancellationToken.None);

        // Assert — succeeds but with empty text
        Assert.Equal(StepOutcomeStatus.Succeeded, outcome.Status);
        var envelope = outcome.Result as Ready.Application.Results.IResultEnvelope;
        Assert.NotNull(envelope);

        var json = System.Text.Json.JsonSerializer.Serialize(envelope.Payload);
        // text should be empty string
        Assert.Contains("\"text\":\"\"", json);
        Assert.Contains("\"chars\":0", json);
    }

    [Fact]
    public async Task ExecuteAsync_StorageThrows_ReturnsFailed()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var docId = Guid.NewGuid();

        db.Documents.Add(new DocumentEntity
        {
            Id = docId,
            CustomerId = "cust-1",
            Source = "upload",
            FileName = "missing.txt",
            ContentType = "text/plain",
            SizeBytes = 10,
            StoragePath = "/data/missing.txt",
            Sha256 = "ghi789",
            Status = 1,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var storage = new FakeFileStorage(); // file not added → GetAsync throws
        var step = new PdfTextExtractStep(db, storage, NullLogger<PdfTextExtractStep>.Instance);
        var ctx = MakeContext(docId);

        // Act
        var outcome = await step.ExecuteAsync(ctx, CancellationToken.None);

        // Assert
        Assert.Equal(StepOutcomeStatus.Failed, outcome.Status);
        Assert.Contains("Extraction failed", outcome.Message);
    }
}
