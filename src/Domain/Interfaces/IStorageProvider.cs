using StorageService.Domain.ValueObjects;

namespace StorageService.Domain.Interfaces;

public interface IStorageProvider
{
    Task<StorageObjectInfo> UploadAsync(
        string bucket,
        string key,
        Stream content,
        string contentType,
        Dictionary<string, string>? metadata = null,
        CancellationToken ct = default);

    Task<Stream> DownloadAsync(
        string bucket,
        string key,
        CancellationToken ct = default);

    Task DeleteAsync(
        string bucket,
        string key,
        CancellationToken ct = default);

    Task<StorageObjectInfo> GetMetadataAsync(
        string bucket,
        string key,
        CancellationToken ct = default);

    Task<bool> ExistsAsync(
        string bucket,
        string key,
        CancellationToken ct = default);

    Task<IReadOnlyList<StorageObjectInfo>> ListAsync(
        string bucket,
        string? prefix = null,
        CancellationToken ct = default);

    Task<string> GetPresignedUrlAsync(
        string bucket,
        string key,
        TimeSpan expiry,
        CancellationToken ct = default);

    Task EnsureBucketExistsAsync(
        string bucket,
        CancellationToken ct = default);
}
