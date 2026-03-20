namespace Storage.Application.Dtos;

public class DocumentBatchMoveResultDto
{
    public int TotalRequested { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public List<DocumentItemMoveResultDto> Results { get; set; } = new();
}

public class DocumentItemMoveResultDto
{
    public string SourceKey { get; set; } = string.Empty;
    public string DestinationKey { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
}