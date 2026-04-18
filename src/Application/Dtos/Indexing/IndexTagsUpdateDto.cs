namespace Storage.Application.Dtos;

public class IndexTagsUpdateDto
{
    public required string Bucket { get; set; }
    public required string Key { get; set; }
    public required Dictionary<string, string> Tags { get; set; }
}