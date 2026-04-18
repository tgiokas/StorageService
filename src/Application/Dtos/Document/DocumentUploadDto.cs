namespace Storage.Application.Dtos;

public class DocumentUploadDto
{
    public required string Bucket { get; set; }
    public required string Key { get; set; }
    public required Stream Content { get; set; }
    public required string ContentType { get; set; }
    public string? UploadedBy { get; set; }

    /// Optional tags for the document index (e.g. department, document_type).
    /// Only used when indexing is enabled.
    public Dictionary<string, string>? Metadata { get; set; }
}
