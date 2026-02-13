using Microsoft.EntityFrameworkCore;
using Ready.Application.Abstractions;
using Ready.Application.DTOs;

namespace Ready.Infrastructure.Persistence;

public sealed class DocumentStore : IDocumentStore
{
    private readonly ReadyDbContext _db;
    public DocumentStore(ReadyDbContext db) => _db = db;

    public async Task<(Guid Id, bool IsNew)> CreateAsync(CreateDocumentRequest request, CancellationToken ct)
    {
        var existing = await _db.Documents
            .Where(x => x.CustomerId == request.CustomerId && x.Sha256 == request.Sha256)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(ct);

        if (existing != Guid.Empty)
        {
            return (existing, false);
        }

        var doc = new DocumentEntity
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            Source = request.Source,
            FileName = request.FileName,
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            StoragePath = request.StoragePath,
            Sha256 = request.Sha256,
            Status = 1, // Stored
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Documents.Add(doc);
        try
        {
            await _db.SaveChangesAsync(ct);
            return (doc.Id, true);
        }
        catch (DbUpdateException)
        {
            // Race condition: another request inserted same (CustomerId, Sha256)
            // Retrieve the existing ID
            var racedId = await _db.Documents
                .Where(x => x.CustomerId == request.CustomerId && x.Sha256 == request.Sha256)
                .Select(x => x.Id)
                .FirstOrDefaultAsync(ct);

            if (racedId != Guid.Empty)
            {
                return (racedId, false);
            }
            throw; // Should not happen if unique constraint is the cause
        }
    }
    
    public async Task<DocumentDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var doc = await _db.Documents.FindAsync([id], ct);
        if (doc is null) return null;
        
        return new DocumentDto(
            doc.Id,
            doc.CustomerId,
            doc.FileName,
            doc.Status,
            doc.CreatedAt
        );
    }

    public async Task<IReadOnlyList<DocumentDto>> ListAsync(string customerId, CancellationToken ct)
    {
        return await _db.Documents
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new DocumentDto(
                x.Id,
                x.CustomerId,
                x.FileName,
                x.Status,
                x.CreatedAt
            ))
            .ToListAsync(ct);
    }
}