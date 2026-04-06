namespace Storage.Application.Dtos;

public class DocumentLocatorDto
{
    public required string Bucket { get; set; }
    public required string Key { get; set; }
}