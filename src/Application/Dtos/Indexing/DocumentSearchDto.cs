namespace Storage.Application.Dtos;

public class DocumentSearchDto
{
    public string SearchText { get; set; } = string.Empty;
    public int? PageNumber { get; set; }
    public int? PageSize { get; set; }
    public string? SortBy { get; set; }
    public bool? SortDescending { get; set; }
}