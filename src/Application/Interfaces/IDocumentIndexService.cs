using StorageService.Application.Dtos;

namespace StorageService.Application.Interfaces;

public interface IDocumentIndexService
{
    Task<Result<DocumentIndexDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<DocumentIndexDto>> GetByBucketAndKeyAsync(string bucket, string key, CancellationToken ct = default);
    Task<Result<PagedResultDto<DocumentIndexDto>>> SearchAsync(DocumentSearchRequestDto request, CancellationToken ct = default);
    Task<Result<DocumentIndexDto>> UpdateTagsAsync(Guid id, Dictionary<string, string> tags, CancellationToken ct = default);
    Task<Result<DocumentIndexDto>> UpdateMetadataAsync(Guid id, Dictionary<string, string> metadata, CancellationToken ct = default);
}
