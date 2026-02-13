using Ready.Application.Abstractions;
using Ready.Infrastructure.Persistence;
using UglyToad.PdfPig;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Ready.Application.Steps; // For ResultEnvelope if needed, or define/use fully qualified if possible. ResultEnvelope is in Ready.Application.Steps namespace, but in Application project. Infra references Application.
using Microsoft.Extensions.Logging;

namespace Ready.Infrastructure.Steps;

public sealed class PdfTextExtractStep : IWorkflowStep
{
    private readonly ReadyDbContext _db;
    private readonly IFileStorage _storage;
    private readonly ILogger<PdfTextExtractStep> _logger;

    public string Name => "pdf.text.extract";

    public PdfTextExtractStep(ReadyDbContext db, IFileStorage storage, ILogger<PdfTextExtractStep> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    public async Task<StepOutcome> ExecuteAsync(StepContext context, CancellationToken ct)
    {
        var doc = await _db.Documents
            .Where(x => x.Id == context.DocumentId)
            .Select(x => new { x.StoragePath, x.ContentType })
            .FirstOrDefaultAsync(ct);

        if (doc == null)
        {
            return StepOutcome.Failed($"Document {context.DocumentId} not found");
        }

        string text = string.Empty;
        
        try 
        {
            await using var stream = await _storage.GetAsync(doc.StoragePath, ct);
            
            if (doc.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                using var pdf = PdfDocument.Open(stream);
                var sb = new StringBuilder();
                foreach (var page in pdf.GetPages())
                {
                    sb.AppendLine(page.Text);
                }
                text = sb.ToString();
            }
            else if (doc.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(stream, Encoding.UTF8);
                text = await reader.ReadToEndAsync(ct);
            }
            else
            {
                _logger.LogWarning("Unsupported content type {ContentType} for text extraction", doc.ContentType);
            }
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Failed to extract text from document {DocumentId}", context.DocumentId);
             return StepOutcome.Failed($"Extraction failed: {ex.Message}");
        }

        var payload = new
        {
            text = text,
            chars = text.Length
        };

        // ResultEnvelope is in Ready.Application.Steps namespace (from previous step 594)
        var result = new ResultEnvelope("DocText", "v1", payload);

        return StepOutcome.Succeeded(result: result);
    }
}
