namespace Storage.Application.Dtos;

public class PagedResultDto<T>
{
    public List<T> Results { get; set; } = new();
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public long Total { get; set; }
    public long Pages => PageSize > 0 ? (long)Math.Ceiling((double)Total / PageSize) : 0;
}