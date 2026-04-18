namespace Storage.Application.Dtos;

public class DocumentIndexDto
{
    public Guid Id { get; set; }
    public string Bucket { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
}