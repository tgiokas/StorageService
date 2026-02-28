namespace StorageService.Application.Dtos;

public class PagedResultDto<T>
{
    public List<T> Results { get; set; } = new();
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public int Pages => PageSize > 0 ? (int)Math.Ceiling((double)Total / PageSize) : 0;
}
