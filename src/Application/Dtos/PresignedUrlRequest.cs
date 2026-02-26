namespace StorageService.Application.Dtos;

public class PresignedUrlRequest
{
    public required string Bucket { get; set; }
    public required string Key { get; set; }
    public int ExpiryMinutes { get; set; } = 60;
}
