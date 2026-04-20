using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Storage.Domain.Interfaces;
using Storage.Domain.Exceptions;
using Storage.Domain.ValueObjects;
using Storage.Application.Configuration;

namespace Storage.Infrastructure.Providers.AzureBlob;

/// Azure Blob Storage provider.
/// Concept mapping:
///   - S3 "bucket"       - Azure Blob "container"
///   - S3 "key"          - Azure Blob "blob name"
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

    public async Task<IReadOnlyList<StorageObjectInfo>> ListObjectsAsync(
        string bucket,
        string? prefix = null,
        CancellationToken ct = default)
    {
        var containerClient = _serviceClient.GetBlobContainerClient(bucket);
        var results = new List<StorageObjectInfo>();

        var options = new GetBlobsByHierarchyOptions
        {
            Prefix = prefix,
            Delimiter = "/",
            Traits = BlobTraits.Metadata
        };

        await foreach (var item in containerClient.GetBlobsByHierarchyAsync(options, ct))
        {
            if (item.IsPrefix)
            {
                results.Add(new StorageObjectInfo
                {
                    Bucket = bucket,
                    Key = item.Prefix,
                    Size = 0,
                    ContentType = string.Empty,
                    ETag = null,
                    LastModified = DateTime.MinValue,
                    Metadata = new Dictionary<string, string>(),
                    IsDir = true
                });
            }
            else if (item.IsBlob)
            {
                var blob = item.Blob;
                var metadata = blob.Metadata?
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string>();

                results.Add(new StorageObjectInfo
                {
                    Bucket = bucket,
                    Key = blob.Name,
                    Size = blob.Properties.ContentLength ?? 0,
                    ContentType = blob.Properties.ContentType ?? string.Empty,
                    ETag = blob.Properties.ETag?.ToString(),
                    LastModified = blob.Properties.LastModified?.UtcDateTime ?? DateTime.MinValue,
                    Metadata = metadata,
                    IsDir = false
                });
            }
        }

        _logger.LogInformation("Listed {Count} items in container {Bucket} with prefix '{Prefix}'",
            results.Count, bucket, prefix);

        return results;
    }

    public async Task<IReadOnlyList<StorageObjectInfo>> ListObjectsAsync(
        string bucket,
        string? prefix = null,
        bool recursive = false,
        CancellationToken ct = default)
    {
        var containerClient = _serviceClient.GetBlobContainerClient(bucket);
        var results = new List<StorageObjectInfo>();

        if (recursive)
        {
            var options = new GetBlobsOptions
            {
                Prefix = prefix 
            };
            
            // Flat listing of every blob under the prefix
            await foreach (var blob in containerClient.GetBlobsAsync(options, ct))
            {
                results.Add(new StorageObjectInfo
                {
                    Bucket = bucket,
                    Key = blob.Name,
                    Size = blob.Properties.ContentLength ?? 0,
                    ContentType = blob.Properties.ContentType ?? string.Empty,
                    ETag = blob.Properties.ETag?.ToString(),
                    LastModified = blob.Properties.LastModified?.UtcDateTime ?? DateTime.MinValue,
                    Metadata = blob.Metadata != null
                        ? new Dictionary<string, string>(blob.Metadata, StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, string>(),
                    IsDir = false
                });
            }
        }
        else
        {
            var options = new GetBlobsByHierarchyOptions
            {
                Prefix = prefix,
                Delimiter = "/",
                Traits = BlobTraits.Metadata
            };

            // Hierarchical listing — returns both blobs and virtual "folders" (prefixes)
            await foreach (var item in containerClient.GetBlobsByHierarchyAsync(options, ct))
            {
                if (item.IsPrefix)
                {
                    // Virtual folder marker
                    results.Add(new StorageObjectInfo
                    {
                        Bucket = bucket,
                        Key = item.Prefix,              // e.g. "dev/temp/"
                        Size = 0,
                        ContentType = string.Empty,
                        ETag = null,
                        LastModified = DateTime.MinValue,
                        Metadata = new Dictionary<string, string>(),
                        IsDir = true
                    });
                }
                else
                {
                    var blob = item.Blob;
                    results.Add(new StorageObjectInfo
                    {
                        Bucket = bucket,
                        Key = blob.Name,
                        Size = blob.Properties.ContentLength ?? 0,
                        ContentType = blob.Properties.ContentType ?? string.Empty,
                        ETag = blob.Properties.ETag?.ToString(),
                        LastModified = blob.Properties.LastModified?.UtcDateTime ?? DateTime.MinValue,
                        Metadata = blob.Metadata != null
                            ? new Dictionary<string, string>(blob.Metadata, StringComparer.OrdinalIgnoreCase)
                            : new Dictionary<string, string>(),
                        IsDir = false
                    });
                }
            }
        }

        _logger.LogInformation("Listed {Count} blobs in container {Bucket} with prefix '{Prefix}' (recursive={Recursive})",
            results.Count, bucket, prefix, recursive);

        return results;
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