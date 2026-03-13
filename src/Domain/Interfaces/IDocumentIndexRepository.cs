using Storage.Domain.Entities;

namespace Storage.Domain.Interfaces;

public interface IDocumentIndexRepository
{
    Task<DocumentIndex?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DocumentIndex?> GetByBucketAndKeyAsync(string bucket, string key, CancellationToken ct = default);
    Task<List<DocumentIndex>> SearchAsync(DocumentIndexQuery query, CancellationToken ct = default);
    Task AddAsync(DocumentIndex document, CancellationToken ct = default);
    Task UpdateAsync(DocumentIndex document, CancellationToken ct = default);
    Task DeleteByBucketAndKeyAsync(string bucket, string key, CancellationToken ct = default);
}