namespace StorageService.Application.Dtos;

public class DocumentUploadDto
{
    public required string Bucket { get; set; }
    public required string Key { get; set; }
    public required Stream Content { get; set; }
    public required string ContentType { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
