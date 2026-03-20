namespace Storage.Application.Dtos;

public class DocumentBatchMoveDto
{
    public required string SourceBucket { get; set; }
    public required string DestinationBucket { get; set; }
    public required List<DocumentItemMoveDto> Items { get; set; }
}

public class DocumentItemMoveDto
{
    public required string SourceKey { get; set; }
    public required string DestinationKey { get; set; }
}