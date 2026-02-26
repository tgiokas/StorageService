using StorageService.Application.Dtos;

namespace StorageService.Application.Interfaces;

public interface IDocumentStorageService
{
    Task<Result<StorageObjectDto>> UploadAsync(UploadDocumentRequest request, CancellationToken ct = default);
    Task<Result<DownloadDocumentResponse>> DownloadAsync(string bucket, string key, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(string bucket, string key, CancellationToken ct = default);
    Task<Result<StorageObjectDto>> GetMetadataAsync(string bucket, string key, CancellationToken ct = default);
    Task<Result<bool>> ExistsAsync(string bucket, string key, CancellationToken ct = default);
    Task<Result<List<StorageObjectDto>>> ListAsync(string bucket, string? prefix = null, CancellationToken ct = default);
    Task<Result<string>> GetPresignedUrlAsync(PresignedUrlRequest request, CancellationToken ct = default);
    Task<Result<bool>> EnsureBucketExistsAsync(string bucket, CancellationToken ct = default);
}
