using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

using Storage.Domain.Interfaces;
using Storage.Domain.Exceptions;
using Storage.Domain.ValueObjects;
using Storage.Application.Configuration;

namespace Storage.Infrastructure.Providers.AzureBlob;

/// Azure Blob Storage provider.
/// Concept mapping:
///   - S3 "bucket"       - Azure Blob "container"
///   - S3 "key"          - Azure Blob "blob name"
/// Custom metadata is stored via <see cref="BlobHttpHeaders"/> and <see cref="BlobUploadOptions.Metadata"/>.
/// Azure normalises user-metadata keys to lowercase; callers should not rely on casing.
public class AzureBlobStorageProvider : IStorageProvider
{
    private readonly BlobServiceClient _serviceClient;
    private readonly ILogger<AzureBlobStorageProvider> _logger;

    public AzureBlobStorageProvider(IOptions<StorageSettings> options, ILogger<AzureBlobStorageProvider> logger)
    {
        _logger = logger;
        var settings = options.Value.AzureBlob;

        _serviceClient = new BlobServiceClient(settings.ConnectionString);

        _logger.LogInformation("Azure Blob storage provider initialized (account: {Account})",
            _serviceClient.AccountName);
    }

    public async Task<StorageObjectInfo> UploadAsync(
        string bucket,
        string key,
        Stream content,
        string contentType,
        Dictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        await EnsureBucketExistsAsync(bucket, ct);

        var originalSize = content.CanSeek ? content.Length : 0;

        var containerClient = _serviceClient.GetBlobContainerClient(bucket);
        var blobClient = containerClient.GetBlobClient(key);

        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            Metadata = metadata
        };

        var response = await blobClient.UploadAsync(content, uploadOptions, ct);

        _logger.LogInformation("Uploaded blob {Key} to container {Bucket} ({Size} bytes)", key, bucket, originalSize);

        return new StorageObjectInfo
        {
            Bucket = bucket,
            Key = key,
            Size = originalSize,
            ContentType = contentType,
            ETag = response.Value.ETag.ToString(),
            LastModified = response.Value.LastModified.UtcDateTime,
            Metadata = metadata ?? new Dictionary<string, string>()
        };
    }

    public async Task<Stream> DownloadAsync(
        string bucket,
        string key,
        CancellationToken ct = default)
    {
        var blobClient = _serviceClient.GetBlobContainerClient(bucket).GetBlobClient(key);

        try
        {
            var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct);

            // Copy to a MemoryStream so the caller gets a fully seekable stream,
            // consistent with MinIO/Garage providers.
            var memoryStream = new MemoryStream();
            await response.Value.Content.CopyToAsync(memoryStream, ct);
            memoryStream.Position = 0;

            _logger.LogInformation("Downloaded blob {Key} from container {Bucket}", key, bucket);

            return memoryStream;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new StorageObjectNotFoundException(bucket, key);
        }
    }

    public async Task DeleteAsync(
        string bucket,
        string key,
        CancellationToken ct = default)
    {
        var blobClient = _serviceClient.GetBlobContainerClient(bucket).GetBlobClient(key);
        
        await blobClient.DeleteIfExistsAsync(cancellationToken: ct);

        _logger.LogInformation("Deleted blob {Key} from container {Bucket}", key, bucket);
    }

    public async Task<StorageObjectInfo> GetMetadataAsync(
        string bucket,
        string key,
        CancellationToken ct = default)
    {
        var blobClient = _serviceClient.GetBlobContainerClient(bucket).GetBlobClient(key);

        try
        {
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: ct);
            var props = properties.Value;

            return new StorageObjectInfo
            {
                Bucket = bucket,
                Key = key,
                Size = props.ContentLength,
                ContentType = props.ContentType,
                ETag = props.ETag.ToString(),
                LastModified = props.LastModified.UtcDateTime,
                Metadata = props.Metadata != null
                    ? new Dictionary<string, string>(props.Metadata, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>()
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new StorageObjectNotFoundException(bucket, key);
        }
    }

    public async Task<bool> ExistsAsync(
        string bucket,
        string key,
        CancellationToken ct = default)
    {
        var blobClient = _serviceClient.GetBlobContainerClient(bucket).GetBlobClient(key);

        try
        {
            var response = await blobClient.GetPropertiesAsync(cancellationToken: ct);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public async Task EnsureBucketExistsAsync(
        string bucket,
        CancellationToken ct = default)
    {
        var containerClient = _serviceClient.GetBlobContainerClient(bucket);

        // CreateIfNotExistsAsync is idempotent — returns null if already exists.
        await containerClient.CreateIfNotExistsAsync(cancellationToken: ct);
    }
}