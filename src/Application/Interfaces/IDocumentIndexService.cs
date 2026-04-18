using Storage.Application.Dtos;

namespace Storage.Application.Interfaces;

public interface IDocumentIndexService
{
    Task<Result<DocumentIndexDto>> GetByBucketAndKeyAsync(string bucket, string key, CancellationToken ct = default);
    Task<Result<PagedResultDto<DocumentIndexDto>>> SearchAsync(DocumentSearchDto request, CancellationToken ct = default);
    Task<Result<DocumentIndexDto>> UpdateTagsAsync(string bucket, string key, Dictionary<string, string> tags, CancellationToken ct = default);
}