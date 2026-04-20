using Storage.Application.Dtos;

namespace Storage.Application.Interfaces;

public interface IDocumentStorageService
{
    Task<Result<IReadOnlyList<StorageObjectDto>>> ListObjectsAsync(string bucket, string? prefix = null, bool recursive = false, CancellationToken ct = default);
    Task<Result<StorageObjectDto>> UploadAsync(DocumentUploadDto request, CancellationToken ct = default);
    Task<Result<DocumentDownloadDto>> DownloadAsync(string bucket, string key, CancellationToken ct = default);
    Task<Result<DocumentBatchDeleteResultDto>> DeleteAsync(List<DocumentLocatorDto> request, CancellationToken ct = default);
    Task<Result<StorageObjectDto>> GetMetadataAsync(string bucket, string key, CancellationToken ct = default);
    Task<Result<DocumentBatchMoveResultDto>> MoveAsync(DocumentBatchMoveDto request, CancellationToken ct = default);
}