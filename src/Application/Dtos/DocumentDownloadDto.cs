namespace StorageService.Application.Dtos;

public class DocumentDownloadDto
{
    public required Stream Content { get; set; }
    public required string ContentType { get; set; }
    public required string FileName { get; set; }
    public long Size { get; set; }
}
