namespace Storage.Application.Dtos;

public class PagedResultDto<T>
{
    public List<T> Results { get; set; } = new();
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public long TotalCount { get; set; }
    public long TotalPages => PageSize > 0 ? (long)Math.Ceiling((double)TotalCount / PageSize) : 1;
}