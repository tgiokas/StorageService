using Microsoft.Extensions.Logging;

using StorageService.Application.Dtos;
using StorageService.Application.Errors;
using StorageService.Application.Interfaces;
using StorageService.Domain.Interfaces;
using StorageService.Domain.ValueObjects;

namespace StorageService.Application.Services;

public class DocumentStorageService : IDocumentStorageService
{
    private readonly IStorageProvider _storageProvider;
    private readonly IErrorCatalog _errors;
    private readonly ILogger<DocumentStorageService> _logger;

    public DocumentStorageService(
        IStorageProvider storageProvider,
        IErrorCatalog errors,
        ILogger<DocumentStorageService> logger)
    {
        _storageProvider = storageProvider;
        _errors = errors;
        _logger = logger;
    }

    public async Task<Result<StorageObjectDto>> UploadAsync(UploadDocumentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Bucket))
            return _errors.Fail<StorageObjectDto>(ErrorCodes.STORAGE.InvalidBucket);

        if (string.IsNullOrWhiteSpace(request.Key))
            return _errors.Fail<StorageObjectDto>(ErrorCodes.STORAGE.InvalidKey);

        if (request.Content == null || request.Content.Length == 0)
            return _errors.Fail<StorageObjectDto>(ErrorCodes.STORAGE.ContentEmpty);

        if (string.IsNullOrWhiteSpace(request.ContentType))
            return _errors.Fail<StorageObjectDto>(ErrorCodes.STORAGE.ContentTypeMissing);

        try
        {
            var result = await _storageProvider.UploadAsync(
                request.Bucket,
                request.Key,
                request.Content,
                request.ContentType,
                request.Metadata,
                ct);

            var dto = MapToDto(result);
            return Result<StorageObjectDto>.Ok(dto, "Document uploaded successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload document {Key} to bucket {Bucket}", request.Key, request.Bucket);
            return _errors.Fail<StorageObjectDto>(ErrorCodes.STORAGE.UploadFailed);
        }
    }

    public async Task<Result<DownloadDocumentResponse>> DownloadAsync(string bucket, string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bucket))
            return _errors.Fail<DownloadDocumentResponse>(ErrorCodes.STORAGE.InvalidBucket);

        if (string.IsNullOrWhiteSpace(key))
            return _errors.Fail<DownloadDocumentResponse>(ErrorCodes.STORAGE.InvalidKey);

        try
        {
            var exists = await _storageProvider.ExistsAsync(bucket, key, ct);
            if (!exists)
                return _errors.Fail<DownloadDocumentResponse>(ErrorCodes.STORAGE.ObjectNotFound);

            var metadata = await _storageProvider.GetMetadataAsync(bucket, key, ct);
            var stream = await _storageProvider.DownloadAsync(bucket, key, ct);

            var response = new DownloadDocumentResponse
            {
                Content = stream,
                ContentType = metadata.ContentType,
                FileName = Path.GetFileName(key),
                Size = metadata.Size
            };

            return Result<DownloadDocumentResponse>.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download document {Key} from bucket {Bucket}", key, bucket);
            return _errors.Fail<DownloadDocumentResponse>(ErrorCodes.STORAGE.DownloadFailed);
        }
    }

    public async Task<Result<bool>> DeleteAsync(string bucket, string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bucket))
            return _errors.Fail<bool>(ErrorCodes.STORAGE.InvalidBucket);

        if (string.IsNullOrWhiteSpace(key))
            return _errors.Fail<bool>(ErrorCodes.STORAGE.InvalidKey);

        try
        {
            var exists = await _storageProvider.ExistsAsync(bucket, key, ct);
            if (!exists)
                return _errors.Fail<bool>(ErrorCodes.STORAGE.ObjectNotFound);

            await _storageProvider.DeleteAsync(bucket, key, ct);
            return Result<bool>.Ok(true, "Document deleted successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document {Key} from bucket {Bucket}", key, bucket);
            return _errors.Fail<bool>(ErrorCodes.STORAGE.DeleteFailed);
        }
    }

    public async Task<Result<StorageObjectDto>> GetMetadataAsync(string bucket, string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bucket))
            return _errors.Fail<StorageObjectDto>(ErrorCodes.STORAGE.InvalidBucket);

        if (string.IsNullOrWhiteSpace(key))
            return _errors.Fail<StorageObjectDto>(ErrorCodes.STORAGE.InvalidKey);

        try
        {
            var exists = await _storageProvider.ExistsAsync(bucket, key, ct);
            if (!exists)
                return _errors.Fail<StorageObjectDto>(ErrorCodes.STORAGE.ObjectNotFound);

            var metadata = await _storageProvider.GetMetadataAsync(bucket, key, ct);
            var dto = MapToDto(metadata);
            return Result<StorageObjectDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get metadata for {Key} in bucket {Bucket}", key, bucket);
            return _errors.Fail<StorageObjectDto>(ErrorCodes.STORAGE.MetadataRetrievalFailed);
        }
    }

    public async Task<Result<bool>> ExistsAsync(string bucket, string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bucket))
            return _errors.Fail<bool>(ErrorCodes.STORAGE.InvalidBucket);

        if (string.IsNullOrWhiteSpace(key))
            return _errors.Fail<bool>(ErrorCodes.STORAGE.InvalidKey);

        try
        {
            var exists = await _storageProvider.ExistsAsync(bucket, key, ct);
            return Result<bool>.Ok(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check existence for {Key} in bucket {Bucket}", key, bucket);
            return _errors.Fail<bool>(ErrorCodes.STORAGE.GenericUnexpected);
        }
    }

    public async Task<Result<List<StorageObjectDto>>> ListAsync(string bucket, string? prefix = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bucket))
            return _errors.Fail<List<StorageObjectDto>>(ErrorCodes.STORAGE.InvalidBucket);

        try
        {
            var objects = await _storageProvider.ListAsync(bucket, prefix, ct);
            var dtos = objects.Select(MapToDto).ToList();
            return Result<List<StorageObjectDto>>.Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list objects in bucket {Bucket} with prefix {Prefix}", bucket, prefix);
            return _errors.Fail<List<StorageObjectDto>>(ErrorCodes.STORAGE.ListObjectsFailed);
        }
    }

    public async Task<Result<string>> GetPresignedUrlAsync(PresignedUrlRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Bucket))
            return _errors.Fail<string>(ErrorCodes.STORAGE.InvalidBucket);

        if (string.IsNullOrWhiteSpace(request.Key))
            return _errors.Fail<string>(ErrorCodes.STORAGE.InvalidKey);

        try
        {
            var expiry = TimeSpan.FromMinutes(request.ExpiryMinutes);
            var url = await _storageProvider.GetPresignedUrlAsync(request.Bucket, request.Key, expiry, ct);
            return Result<string>.Ok(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate presigned URL for {Key} in bucket {Bucket}", request.Key, request.Bucket);
            return _errors.Fail<string>(ErrorCodes.STORAGE.PresignedUrlFailed);
        }
    }

    public async Task<Result<bool>> EnsureBucketExistsAsync(string bucket, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bucket))
            return _errors.Fail<bool>(ErrorCodes.STORAGE.InvalidBucket);

        try
        {
            await _storageProvider.EnsureBucketExistsAsync(bucket, ct);
            return Result<bool>.Ok(true, $"Bucket '{bucket}' is ready.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure bucket {Bucket} exists", bucket);
            return _errors.Fail<bool>(ErrorCodes.STORAGE.BucketCreationFailed);
        }
    }

    private static StorageObjectDto MapToDto(StorageObjectInfo info) => new()
    {
        Bucket = info.Bucket,
        Key = info.Key,
        Size = info.Size,
        ContentType = info.ContentType,
        ETag = info.ETag,
        LastModified = info.LastModified,
        Metadata = info.Metadata
    };
}
