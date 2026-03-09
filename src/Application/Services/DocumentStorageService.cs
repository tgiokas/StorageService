using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Storage.Application.Configuration;
using Storage.Application.Dtos;
using Storage.Application.Errors;
using Storage.Application.Interfaces;
using Storage.Domain.Entities;
using Storage.Domain.Exceptions;
using Storage.Domain.Interfaces;
using Storage.Domain.ValueObjects;

namespace Storage.Application.Services;

public class DocumentStorageService : IDocumentStorageService
{
    private readonly IStorageProvider _storageProvider;
    private readonly IndexingSettings _indexingSettings;
    private readonly IErrorCatalog _errors;
    private readonly ILogger<DocumentStorageService> _logger;
    private readonly IDocumentIndexRepository? _indexRepository;

    public DocumentStorageService(
        IStorageProvider storageProvider,
        IOptions<IndexingSettings> indexingSettings,
        IErrorCatalog errors,
        ILogger<DocumentStorageService> logger,
        IDocumentIndexRepository? indexRepository = null)
    {
        _storageProvider = storageProvider;
        _indexingSettings = indexingSettings.Value;
        _errors = errors;
        _logger = logger;
        _indexRepository = indexRepository;
    }

    private bool IsIndexingEnabled => _indexingSettings.Enabled && _indexRepository is not null;

    public async Task<Result<StorageObjectDto>> UploadAsync(DocumentUploadDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Bucket))
            return _errors.Fail<StorageObjectDto>(ErrorCodes.STORAGE.InvalidBucket);

        if (string.IsNullOrWhiteSpace(request.Key))
            return _errors.Fail<StorageObjectDto>(ErrorCodes.STORAGE.InvalidKey);

        if (request.Content is null || request.Content.Length == 0)
            return _errors.Fail<StorageObjectDto>(ErrorCodes.STORAGE.ContentEmpty);

        if (string.IsNullOrWhiteSpace(request.ContentType))
            return _errors.Fail<StorageObjectDto>(ErrorCodes.STORAGE.ContentTypeMissing);

        try
        {
            // Business rule: Bucket + Key is the identity, duplicates are rejected
            var exists = await _storageProvider.ExistsAsync(request.Bucket, request.Key, ct);
            if (exists)
            {
                _logger.LogWarning("Rejected duplicate upload for {Bucket}/{Key}", request.Bucket, request.Key);
                return _errors.Fail<StorageObjectDto>(ErrorCodes.STORAGE.ObjectAlreadyExists);
            }

            // Prefix tag keys so they don't collide with system metadata (e.g. x-encrypted)
            Dictionary<string, string>? objectMetadata = null;
            if (request.Tags != null && request.Tags.Count > 0)
            {
                objectMetadata = request.Tags
                    .ToDictionary(kvp => $"x-tag-{kvp.Key}", kvp => kvp.Value);
            }

            var storageObject = await _storageProvider.UploadAsync(
                request.Bucket,
                request.Key,
                request.Content,
                request.ContentType,
                objectMetadata,
                ct);

            try
            {
                if (IsIndexingEnabled)
                {
                    await CreateIndexEntryAsync(request, storageObject, ct);
                }
            }
            catch (Exception indexEx)
            {
                _logger.LogError(indexEx, "Indexing failed after upload for {Bucket}/{Key}. Rolling back object storage.", request.Bucket, request.Key);

                try
                {
                    await _storageProvider.DeleteAsync(request.Bucket, request.Key, ct);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Rollback delete also failed for {Bucket}/{Key}", request.Bucket, request.Key);
                }

                return _errors.Fail<StorageObjectDto>(ErrorCodes.STORAGE.UploadFailed);
            }

            var dto = MapToDto(storageObject);

            return Result<StorageObjectDto>.Ok(dto, "Document uploaded successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload document {Key} to bucket {Bucket}", request.Key, request.Bucket);
            return _errors.Fail<StorageObjectDto>(ErrorCodes.STORAGE.UploadFailed);
        }
    }

    public async Task<Result<DocumentDownloadDto>> DownloadAsync(string bucket, string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bucket))
            return _errors.Fail<DocumentDownloadDto>(ErrorCodes.STORAGE.InvalidBucket);

        if (string.IsNullOrWhiteSpace(key))
            return _errors.Fail<DocumentDownloadDto>(ErrorCodes.STORAGE.InvalidKey);

        try
        {
            var metadata = await _storageProvider.GetMetadataAsync(bucket, key, ct);
            var stream = await _storageProvider.DownloadAsync(bucket, key, ct);

            var response = new DocumentDownloadDto
            {
                Content = stream,
                ContentType = metadata.ContentType,
                FileName = Path.GetFileName(key),
                Size = metadata.Size
            };

            return Result<DocumentDownloadDto>.Ok(response);
        }
        catch (StorageObjectNotFoundException)
        {
            return _errors.Fail<DocumentDownloadDto>(ErrorCodes.STORAGE.ObjectNotFound);
        }
        catch (StorageDecryptionException ex)
        {
            _logger.LogError(ex, "Decryption failed for document {Key} in bucket {Bucket}", key, bucket);
            return _errors.Fail<DocumentDownloadDto>(ErrorCodes.STORAGE.DecryptionFailed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download document {Key} from bucket {Bucket}", key, bucket);
            return _errors.Fail<DocumentDownloadDto>(ErrorCodes.STORAGE.DownloadFailed);
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

            if (IsIndexingEnabled)
            {
                try
                {
                    await _indexRepository!.DeleteByBucketAndKeyAsync(bucket, key, ct);
                    _logger.LogInformation("Removed index entry for {Bucket}/{Key}", bucket, key);
                }
                catch (Exception indexEx)
                {
                    _logger.LogWarning(indexEx, "Object deletion succeeded but index deletion failed for {Bucket}/{Key}", bucket, key);
                }
            }

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

    public async Task<Result<string>> GetPresignedUrlAsync(PresignedUrlDto request, CancellationToken ct = default)
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

    private async Task CreateIndexEntryAsync(DocumentUploadDto request, StorageObjectInfo metadata, CancellationToken ct)
    {
        var indexEntry = new DocumentIndex
        {
            Id = Guid.NewGuid(),
            Bucket = metadata.Bucket,
            Key = metadata.Key,
            FileName = Path.GetFileName(request.Key),
            ContentType = metadata.ContentType,
            Size = metadata.Size,
            IsEncrypted = metadata.Metadata.ContainsKey("x-encrypted"),
            UploadedBy = request.UploadedBy,
            UploadedAt = DateTime.UtcNow,
            Tags = request.Tags ?? new Dictionary<string, string>()
        };

        await _indexRepository!.AddAsync(indexEntry, ct);

        _logger.LogInformation("Created index entry for {Bucket}/{Key}", metadata.Bucket, metadata.Key);
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
            .Where(kvp => !kvp.Key.Equals("x-encrypted", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                kvp => kvp.Key.StartsWith("x-tag-", StringComparison.OrdinalIgnoreCase)
                    ? kvp.Key[6..]
                    : kvp.Key,
                kvp => kvp.Value)
    };
}
