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

    private const int MaxBatchMoveItems = 100;

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
            if (request.Metadata != null && request.Metadata.Count > 0)
            {
                objectMetadata = request.Metadata
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
            var metadata = await _storageProvider.GetMetadataAsync(bucket, key, ct);
            var dto = MapToDto(metadata);

            return Result<StorageObjectDto>.Ok(dto);
        }
        catch (StorageObjectNotFoundException)
        {
            return _errors.Fail<StorageObjectDto>(ErrorCodes.STORAGE.ObjectNotFound);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get metadata for {Key} in bucket {Bucket}", key, bucket);
            return _errors.Fail<StorageObjectDto>(ErrorCodes.STORAGE.MetadataRetrievalFailed);
        }
    }
        
    public async Task<Result<DocumentBatchMoveResultDto>> MoveAsync(DocumentBatchMoveDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.SourceBucket))
            return _errors.Fail<DocumentBatchMoveResultDto>(ErrorCodes.STORAGE.InvalidBucket);

        if (string.IsNullOrWhiteSpace(request.DestinationBucket))
            return _errors.Fail<DocumentBatchMoveResultDto>(ErrorCodes.STORAGE.InvalidBucket);

        if (request.Items is null || request.Items.Count == 0)
            return _errors.Fail<DocumentBatchMoveResultDto>(ErrorCodes.STORAGE.InvalidKey);

        if (request.Items.Count > MaxBatchMoveItems)
            return _errors.Fail<DocumentBatchMoveResultDto>(ErrorCodes.STORAGE.BatchLimitExceeded);

        var resultDto = new DocumentBatchMoveResultDto
        {
            TotalRequested = request.Items.Count
        };

        foreach (var item in request.Items)
        {
            var itemResult = await MoveSingleAsync(
                request.SourceBucket, item.SourceKey,
                request.DestinationBucket, item.DestinationKey,
                ct);

            resultDto.Results.Add(itemResult);

            if (itemResult.Success)
                resultDto.Succeeded++;
            else
                resultDto.Failed++;
        }

        var message = resultDto.Failed == 0
            ? "All documents moved successfully."
            : $"{resultDto.Succeeded} of {resultDto.TotalRequested} documents moved successfully.";

        return Result<DocumentBatchMoveResultDto>.Ok(resultDto, message);
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
            Tags = request.Metadata ?? new Dictionary<string, string>()
        };

        await _indexRepository!.AddAsync(indexEntry, ct);

        _logger.LogInformation("Created index entry for {Bucket}/{Key}", metadata.Bucket, metadata.Key);
    }

    private async Task<DocumentItemMoveResultDto> MoveSingleAsync(
        string sourceBucket, string sourceKey,
        string destinationBucket, string destinationKey,
        CancellationToken ct)
    {
        var result = new DocumentItemMoveResultDto
        {
            SourceKey = sourceKey,
            DestinationKey = destinationKey
        };

        if (string.IsNullOrWhiteSpace(sourceKey) || string.IsNullOrWhiteSpace(destinationKey))
        {
            result.Error = "Source key and destination key are required.";
            result.ErrorCode = ErrorCodes.STORAGE.InvalidKey;
            return result;
        }

        // Same bucket + same key = no-op
        if (sourceBucket == destinationBucket && sourceKey == destinationKey)
        {
            result.Success = true;
            return result;
        }

        try
        {
            // 1. Verify source exists
            var sourceExists = await _storageProvider.ExistsAsync(sourceBucket, sourceKey, ct);
            if (!sourceExists)
            {
                result.Error = $"Source object '{sourceBucket}/{sourceKey}' not found.";
                result.ErrorCode = ErrorCodes.STORAGE.ObjectNotFound;
                return result;
            }

            // 2. Verify destination does not already exist (duplicate-rejection)
            var destinationExists = await _storageProvider.ExistsAsync(destinationBucket, destinationKey, ct);
            if (destinationExists)
            {
                result.Error = $"Destination object '{destinationBucket}/{destinationKey}' already exists.";
                result.ErrorCode = ErrorCodes.STORAGE.ObjectAlreadyExists;
                return result;
            }

            // 3. Download source (goes through encryption decorator — returns decrypted stream)
            var sourceMetadata = await _storageProvider.GetMetadataAsync(sourceBucket, sourceKey, ct);
            using var sourceStream = await _storageProvider.DownloadAsync(sourceBucket, sourceKey, ct);

            // 4. Upload to destination (re-encrypts transparently via decorator)
            await _storageProvider.UploadAsync(
                destinationBucket,
                destinationKey,
                sourceStream,
                sourceMetadata.ContentType,
                sourceMetadata.Metadata.Count > 0 ? new Dictionary<string, string>(sourceMetadata.Metadata) : null,
                ct);

            // 5. Update Elasticsearch index entry if indexing is enabled
            if (IsIndexingEnabled)
            {
                try
                {
                    var indexEntry = await _indexRepository!.GetByBucketAndKeyAsync(sourceBucket, sourceKey, ct);
                    if (indexEntry is not null)
                    {
                        indexEntry.Bucket = destinationBucket;
                        indexEntry.Key = destinationKey;
                        indexEntry.FileName = Path.GetFileName(destinationKey);
                        indexEntry.ModifiedAt = DateTime.UtcNow;
                        await _indexRepository.UpdateAsync(indexEntry, ct);
                    }
                }
                catch (Exception indexEx)
                {
                    // Index update failed — roll back the destination copy
                    _logger.LogError(indexEx,
                        "Index update failed during move of {SourceBucket}/{SourceKey} → {DestBucket}/{DestKey}. Rolling back destination copy.",
                        sourceBucket, sourceKey, destinationBucket, destinationKey);

                    await RollbackDestinationAsync(destinationBucket, destinationKey, ct);

                    result.Error = "Move failed: could not update document index.";
                    result.ErrorCode = ErrorCodes.STORAGE.MoveFailed;
                    return result;
                }
            }

            // 6. Delete the source object
            try
            {
                await _storageProvider.DeleteAsync(sourceBucket, sourceKey, ct);
            }
            catch (Exception deleteEx)
            {
                // Source delete failed — object now exists in both locations, index points to destination.
                // Log as warning
                _logger.LogWarning(deleteEx,
                    "Source deletion failed after successful move of {SourceBucket}/{SourceKey} → {DestBucket}/{DestKey}. Source object is now orphaned.",
                    sourceBucket, sourceKey, destinationBucket, destinationKey);
            }

            result.Success = true;
            _logger.LogInformation("Moved document {SourceBucket}/{SourceKey} → {DestBucket}/{DestKey}",
                sourceBucket, sourceKey, destinationBucket, destinationKey);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move document {SourceBucket}/{SourceKey} → {DestBucket}/{DestKey}",
                sourceBucket, sourceKey, destinationBucket, destinationKey);

            result.Error = "An unexpected error occurred during move.";
            result.ErrorCode = ErrorCodes.STORAGE.MoveFailed;
            return result;
        }
    }

    private async Task RollbackDestinationAsync(string bucket, string key, CancellationToken ct)
    {
        try
        {
            await _storageProvider.DeleteAsync(bucket, key, ct);
            _logger.LogInformation("Rolled back destination copy {Bucket}/{Key}", bucket, key);
        }
        catch (Exception rollbackEx)
        {
            _logger.LogError(rollbackEx, "Rollback of destination copy also failed for {Bucket}/{Key}", bucket, key);
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
            .Where(kvp => !kvp.Key.Equals("x-encrypted", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                kvp => kvp.Key.StartsWith("x-tag-", StringComparison.OrdinalIgnoreCase)
                    ? kvp.Key[6..]
                    : kvp.Key,
                kvp => kvp.Value)
    };
}
