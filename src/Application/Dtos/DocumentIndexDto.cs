namespace StorageService.Application.Dtos;

public class DocumentIndexDto
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
    public DateTime UploadedAt { get; set; }
    public DateTime? LastModified { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
    public Dictionary<string, string> CustomMetadata { get; set; } = new();
}
