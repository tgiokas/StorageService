namespace StorageService.Application.Dtos;

public class DocumentSearchRequestDto
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
