namespace Storage.Application.Dtos;

public class DocumentBatchDeleteResultDto
{
    public int TotalRequested { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public List<DocumentItemDeleteResultDto> Results { get; set; } = new();
}

public class DocumentItemDeleteResultDto
{
    public string Bucket { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
}