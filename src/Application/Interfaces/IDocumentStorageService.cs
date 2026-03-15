using Storage.Application.Dtos;

namespace Storage.Application.Interfaces;

public interface IDocumentStorageService
{
    Task<Result<StorageObjectDto>> UploadAsync(DocumentUploadDto request, CancellationToken ct = default);
    Task<Result<DocumentDownloadDto>> DownloadAsync(string bucket, string key, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(string bucket, string key, CancellationToken ct = default);
    Task<Result<StorageObjectDto>> GetMetadataAsync(string bucket, string key, CancellationToken ct = default);
}
