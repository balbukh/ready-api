using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Ready.Application.Abstractions;
using Ready.Application.AI;
using Ready.Application.DTOs;
using Ready.Application.Results;
using Ready.Application.Steps;
using Xunit;

namespace Ready.UnitTests;

public class InvoiceExtractV1StepTests
{
    private static StepContext MakeContext(Guid runId) =>
        new(Guid.NewGuid(), "cust-1", "invoice", "v1", Guid.NewGuid(), runId);

    // ---- Fakes ----

    private sealed class FakeOpenAiClient : IOpenAiClient
    {
        public string? Response { get; set; }
        public Exception? ExceptionToThrow { get; set; }

        public Task<string> GenerateJsonAsync(string instructions, string inputText, CancellationToken ct)
        {
            if (ExceptionToThrow is not null) throw ExceptionToThrow;
            return Task.FromResult(Response ?? "{}");
        }
    }

    private sealed class FakeResultStore : IResultStore
    {
        private readonly List<(Guid RunId, string ResultType, string Version, object Payload)> _results = new();

        public void Seed(Guid runId, string resultType, string version, object payload)
            => _results.Add((runId, resultType, version, payload));

        public Task SaveAsync(Guid runId, string resultType, string version, object payload, CancellationToken ct)
        {
            _results.Add((runId, resultType, version, payload));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WorkflowResultDto>> GetResultsAsync(Guid runId, CancellationToken ct)
        {
            var list = _results
                .Where(r => r.RunId == runId)
                .Select(r => new WorkflowResultDto(r.ResultType, r.Version, r.Payload, DateTimeOffset.UtcNow))
                .ToList();
            return Task.FromResult<IReadOnlyList<WorkflowResultDto>>(list);
        }
    }

    // ---- Tests ----

    [Fact]
    public async Task ExecuteAsync_MissingDocText_ReturnsFailed()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var ai = new FakeOpenAiClient();
        var results = new FakeResultStore(); // empty — no DocText
        var step = new InvoiceExtractV1Step(ai, results, NullLogger<InvoiceExtractV1Step>.Instance);
        var ctx = MakeContext(runId);

        // Act
        var outcome = await step.ExecuteAsync(ctx, CancellationToken.None);

        // Assert
        Assert.Equal(StepOutcomeStatus.Failed, outcome.Status);
        Assert.Contains("No document text", outcome.Message);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyText_ReturnsFailed()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var ai = new FakeOpenAiClient();
        var results = new FakeResultStore();
        results.Seed(runId, "DocText", "v1", new { text = "", chars = 0 });

        var step = new InvoiceExtractV1Step(ai, results, NullLogger<InvoiceExtractV1Step>.Instance);
        var ctx = MakeContext(runId);

        // Act
        var outcome = await step.ExecuteAsync(ctx, CancellationToken.None);

        // Assert — empty text now returns Failed
        Assert.Equal(StepOutcomeStatus.Failed, outcome.Status);
        Assert.Contains("No document text", outcome.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ValidResponse_ReturnsInvoiceExtract()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var invoiceJson = JsonSerializer.Serialize(new
        {
            invoiceNumber = "INV-001",
            date = "2024-02-12",
            vendorName = "Acme Corp",
            total = 121.00
        });

        var ai = new FakeOpenAiClient { Response = invoiceJson };
        var results = new FakeResultStore();
        results.Seed(runId, "DocText", "v1", new { text = "some invoice text", chars = 17 });

        var step = new InvoiceExtractV1Step(ai, results, NullLogger<InvoiceExtractV1Step>.Instance);
        var ctx = MakeContext(runId);

        // Act
        var outcome = await step.ExecuteAsync(ctx, CancellationToken.None);

        // Assert
        Assert.Equal(StepOutcomeStatus.Succeeded, outcome.Status);
        var envelope = outcome.Result as IResultEnvelope;
        Assert.NotNull(envelope);
        Assert.Equal("InvoiceExtract", envelope.ResultType);
        Assert.Equal("v1", envelope.Version);
    }

    [Fact]
    public async Task ExecuteAsync_OpenAiThrows_ReturnsFailed()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var ai = new FakeOpenAiClient { ExceptionToThrow = new HttpRequestException("timeout") };
        var results = new FakeResultStore();
        results.Seed(runId, "DocText", "v1", new { text = "some text", chars = 9 });

        var step = new InvoiceExtractV1Step(ai, results, NullLogger<InvoiceExtractV1Step>.Instance);
        var ctx = MakeContext(runId);

        // Act
        var outcome = await step.ExecuteAsync(ctx, CancellationToken.None);

        // Assert
        Assert.Equal(StepOutcomeStatus.Failed, outcome.Status);
        Assert.Contains("OpenAI failure", outcome.Message);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidJson_ReturnsFailed()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var ai = new FakeOpenAiClient { Response = "not json at all{{{" };
        var results = new FakeResultStore();
        results.Seed(runId, "DocText", "v1", new { text = "some text", chars = 9 });

        var step = new InvoiceExtractV1Step(ai, results, NullLogger<InvoiceExtractV1Step>.Instance);
        var ctx = MakeContext(runId);

        // Act
        var outcome = await step.ExecuteAsync(ctx, CancellationToken.None);

        // Assert
        Assert.Equal(StepOutcomeStatus.Failed, outcome.Status);
        Assert.Contains("Invalid JSON", outcome.Message);
    }

    [Fact]
    public async Task ExecuteAsync_PartialFields_StillReturnsSucceeded()
    {
        // The step trusts the AI prompt to fill fields; it does not validate required fields server-side.
        // Partial JSON is still valid JSON, so it should succeed.
        var runId = Guid.NewGuid();
        var partialJson = JsonSerializer.Serialize(new
        {
            invoiceNumber = "INV-001",
            invoiceDate = (string?)null,
            vendorName = (string?)null,
            total = (decimal?)null
        });

        var ai = new FakeOpenAiClient { Response = partialJson };
        var results = new FakeResultStore();
        results.Seed(runId, "DocText", "v1", new { text = "some text", chars = 9 });

        var step = new InvoiceExtractV1Step(ai, results, NullLogger<InvoiceExtractV1Step>.Instance);
        var ctx = MakeContext(runId);

        // Act
        var outcome = await step.ExecuteAsync(ctx, CancellationToken.None);

        // Assert
        Assert.Equal(StepOutcomeStatus.Succeeded, outcome.Status);
        var envelope = outcome.Result as IResultEnvelope;
        Assert.NotNull(envelope);
        Assert.Equal("InvoiceExtract", envelope.ResultType);
    }
}
