using StorageService.Domain.Entities;

namespace StorageService.Domain.Interfaces;

public interface IDocumentIndexRepository
{
    Task<DocumentIndex?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DocumentIndex?> GetByBucketAndKeyAsync(string bucket, string key, CancellationToken ct = default);
    Task<List<DocumentIndex>> SearchAsync(DocumentIndexQuery query, CancellationToken ct = default);
    Task<int> CountAsync(DocumentIndexQuery query, CancellationToken ct = default);
    Task AddAsync(DocumentIndex document, CancellationToken ct = default);
    Task UpdateAsync(DocumentIndex document, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task DeleteByBucketAndKeyAsync(string bucket, string key, CancellationToken ct = default);
}

/// Query object for searching the document index.
/// All filter fields are optional — null means "don't filter on this field".
public class DocumentIndexQuery
{
    public string? Bucket { get; set; }
    public string? KeyPrefix { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime? UploadedFrom { get; set; }
    public DateTime? UploadedTo { get; set; }
    public Dictionary<string, string>? Tags { get; set; }

    // Pagination
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;

    // Sorting
    public string SortBy { get; set; } = "UploadedAt";
    public bool SortDescending { get; set; } = true;
}
