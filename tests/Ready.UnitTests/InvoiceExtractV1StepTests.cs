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

        public Task<string> GenerateJsonAsync(string instructions, string userPrompt, Guid? runId = null, Guid? docId = null, Guid? corrId = null, CancellationToken ct = default)
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

        public bool HasErrorResult(Guid runId) 
            => _results.Any(r => r.RunId == runId && r.ResultType == "InvoiceExtractError");
    }

    // ---- Tests ----

    [Fact]
    public async Task ExecuteAsync_MissingDocText_ReturnsFailed()
    {
        var runId = Guid.NewGuid();
        var ai = new FakeOpenAiClient();
        var results = new FakeResultStore(); // empty
        var step = new InvoiceExtractV1Step(ai, results, NullLogger<InvoiceExtractV1Step>.Instance);
        var ctx = MakeContext(runId);

        var outcome = await step.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(StepOutcomeStatus.Failed, outcome.Status);
        Assert.Contains("No document text", outcome.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ValidResponse_ReturnsInvoiceExtract()
    {
        var runId = Guid.NewGuid();
        var invoiceJson = JsonSerializer.Serialize(new
        {
            invoiceNumber = "INV-001",
            invoiceDate = "2024-02-12",
            // Missing sellerName but has InvoiceNumber -> Valid identity
            sellerName = (string?)null, 
            total = 121.00,
            currency = "EUR"
        });

        var ai = new FakeOpenAiClient { Response = invoiceJson };
        var results = new FakeResultStore();
        results.Seed(runId, "DocText", "v1", new { text = "text", chars = 10 });

        var step = new InvoiceExtractV1Step(ai, results, NullLogger<InvoiceExtractV1Step>.Instance);
        var ctx = MakeContext(runId);

        var outcome = await step.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(StepOutcomeStatus.Succeeded, outcome.Status);
        var envelope = outcome.Result as IResultEnvelope;
        Assert.NotNull(envelope);
        Assert.Equal("InvoiceExtract", envelope.ResultType);
        Assert.False(results.HasErrorResult(runId));
    }

    [Fact]
    public async Task ExecuteAsync_InvalidJson_PersistsErrorAndReturnsFailed()
    {
        var runId = Guid.NewGuid();
        var ai = new FakeOpenAiClient { Response = "not json{{{" };
        var results = new FakeResultStore();
        results.Seed(runId, "DocText", "v1", new { text = "text", chars = 10 });

        var step = new InvoiceExtractV1Step(ai, results, NullLogger<InvoiceExtractV1Step>.Instance);
        var ctx = MakeContext(runId);

        var outcome = await step.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(StepOutcomeStatus.Failed, outcome.Status);
        Assert.Contains("invalid json", outcome.Message?.ToLower());
        Assert.True(results.HasErrorResult(runId), "Should persist InvoiceExtractError");
    }

    [Fact]
    public async Task ExecuteAsync_ValidationFailure_PersistsErrorAndReturnsFailed()
    {
        var runId = Guid.NewGuid();
        // Invalid: Missing total, Invalid Currency
        var invalidJson = JsonSerializer.Serialize(new
        {
            invoiceNumber = "INV-001",
            currency = "euro", // Invalid regex
            // total missing
        });

        var ai = new FakeOpenAiClient { Response = invalidJson };
        var results = new FakeResultStore();
        results.Seed(runId, "DocText", "v1", new { text = "text", chars = 10 });

        var step = new InvoiceExtractV1Step(ai, results, NullLogger<InvoiceExtractV1Step>.Instance);
        var ctx = MakeContext(runId);

        var outcome = await step.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(StepOutcomeStatus.Failed, outcome.Status);
        Assert.Contains("validation failed", outcome.Message?.ToLower());
        Assert.True(results.HasErrorResult(runId), "Should persist InvoiceExtractError");
    }

    [Fact]
    public async Task ExecuteAsync_MissingIdentity_ReturnsFailed()
    {
        var runId = Guid.NewGuid();
        // Valid total/currency but missing invoiceNumber AND (invoiceDate+sellerName)
        var json = JsonSerializer.Serialize(new
        {
            total = 100.00,
            currency = "USD",
            invoiceNumber = (string?)null,
            invoiceDate = "2024-01-01",
            sellerName = (string?)null
        });

        var ai = new FakeOpenAiClient { Response = json };
        var results = new FakeResultStore();
        results.Seed(runId, "DocText", "v1", new { text = "text", chars = 10 });

        var step = new InvoiceExtractV1Step(ai, results, NullLogger<InvoiceExtractV1Step>.Instance);
        var outcome = await step.ExecuteAsync(MakeContext(runId), CancellationToken.None);

        Assert.Equal(StepOutcomeStatus.Failed, outcome.Status);
        Assert.True(results.HasErrorResult(runId));
    }
}
