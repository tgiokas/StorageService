namespace StorageService.Application.Dtos;

public class StorageObjectDto
{
    public string Bucket { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public long Size { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string? ETag { get; set; }
    public DateTime LastModified { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
