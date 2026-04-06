namespace Storage.Application.Dtos;

public class TagsUpdateDto
{
    public required Guid Id { get; set; }
    public required Dictionary<string, string> Tags { get; set; }
}