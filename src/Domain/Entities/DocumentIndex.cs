namespace StorageService.Domain.Entities;

public class DocumentIndex
{
    public Guid Id { get; set; }
    public string Bucket { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string? ETag { get; set; }
    public bool IsEncrypted { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastModified { get; set; }

    /// Key-value tags for categorization and filtering.
    /// Stored as JSON in Postgres.
    public Dictionary<string, string> Tags { get; set; } = new();

    /// Free-form custom metadata.
    /// Stored as JSON in Postgres.
    public Dictionary<string, string> CustomMetadata { get; set; } = new();
}
