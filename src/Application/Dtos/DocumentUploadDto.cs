namespace Storage.Application.Dtos;

public class DocumentUploadDto
{
    public required string Bucket { get; set; }
    public required string Key { get; set; }
    public required Stream Content { get; set; }
    public required string ContentType { get; set; }

    /// Storage provider headers (e.g. content-disposition). Passed to MinIO/Azure as HTTP headers.
    public Dictionary<string, string>? Metadata { get; set; }

    /// Optional tags for the document index (e.g. department, document_type).
    /// Only used when indexing is enabled.
    public Dictionary<string, string>? Tags { get; set; }
}
