using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Minio;
using Minio.DataModel.Args;

using StorageService.Domain.Interfaces;
using StorageService.Domain.ValueObjects;
using StorageService.Infrastructure.Configuration;

namespace StorageService.Infrastructure.Providers.MinIO;

public class MinioStorageProvider : IStorageProvider
{
    private readonly IMinioClient _client;
    private readonly ILogger<MinioStorageProvider> _logger;

    public MinioStorageProvider(IOptions<StorageSettings> options, ILogger<MinioStorageProvider> logger)
    {
        _logger = logger;
        var settings = options.Value.MinIO;

        var builder = new MinioClient()
            .WithEndpoint(settings.Endpoint)
            .WithCredentials(settings.AccessKey, settings.SecretKey);

        if (settings.UseSsl)
            builder = builder.WithSSL();

        _client = builder.Build();

        _logger.LogInformation("MinIO storage provider initialized with endpoint: {Endpoint}", settings.Endpoint);
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

        var putArgs = new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)
            .WithStreamData(content)
            .WithObjectSize(content.Length)
            .WithContentType(contentType);

        if (metadata != null && metadata.Count > 0)
        {
            putArgs = putArgs.WithHeaders(metadata);
        }

        var response = await _client.PutObjectAsync(putArgs, ct);

        _logger.LogInformation("Uploaded object {Key} to bucket {Bucket} ({Size} bytes)", key, bucket, response.Size);

        return new StorageObjectInfo
        {
            Bucket = bucket,
            Key = key,
            Size = response.Size,
            ContentType = contentType,
            ETag = response.Etag,
            LastModified = DateTime.UtcNow,
            Metadata = metadata ?? new Dictionary<string, string>()
        };
    }

    public async Task<Stream> DownloadAsync(
        string bucket,
        string key,
        CancellationToken ct = default)
    {
        var memoryStream = new MemoryStream();

        var getArgs = new GetObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)
            .WithCallbackStream(async (stream, cancellationToken) =>
            {
                await stream.CopyToAsync(memoryStream, cancellationToken);
            });

        await _client.GetObjectAsync(getArgs, ct);

        memoryStream.Position = 0;

        _logger.LogInformation("Downloaded object {Key} from bucket {Bucket}", key, bucket);

        return memoryStream;
    }

    public async Task DeleteAsync(
        string bucket,
        string key,
        CancellationToken ct = default)
    {
        var removeArgs = new RemoveObjectArgs()
            .WithBucket(bucket)
            .WithObject(key);

        await _client.RemoveObjectAsync(removeArgs, ct);

        _logger.LogInformation("Deleted object {Key} from bucket {Bucket}", key, bucket);
    }

    public async Task<StorageObjectInfo> GetMetadataAsync(
        string bucket,
        string key,
        CancellationToken ct = default)
    {
        var statArgs = new StatObjectArgs()
            .WithBucket(bucket)
            .WithObject(key);

        var stat = await _client.StatObjectAsync(statArgs, ct);

        return new StorageObjectInfo
        {
            Bucket = bucket,
            Key = key,
            Size = stat.Size,
            ContentType = stat.ContentType,
            ETag = stat.ETag,
            LastModified = stat.LastModified,
            Metadata = stat.MetaData != null
                ? new Dictionary<string, string>(stat.MetaData, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>()
        };
    }

    public async Task<bool> ExistsAsync(
        string bucket,
        string key,
        CancellationToken ct = default)
    {
        try
        {
            var statArgs = new StatObjectArgs()
                .WithBucket(bucket)
                .WithObject(key);

            await _client.StatObjectAsync(statArgs, ct);
            return true;
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            return false;
        }
        catch (Minio.Exceptions.BucketNotFoundException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<StorageObjectInfo>> ListAsync(
        string bucket,
        string? prefix = null,
        CancellationToken ct = default)
    {
        var results = new List<StorageObjectInfo>();

        var listArgs = new ListObjectsArgs()
            .WithBucket(bucket)
            .WithRecursive(true);

        if (!string.IsNullOrWhiteSpace(prefix))
        {
            listArgs = listArgs.WithPrefix(prefix);
        }

        await foreach (var item in _client.ListObjectsEnumAsync(listArgs, ct))
        {
            if (!item.IsDir)
            {
                results.Add(new StorageObjectInfo
                {
                    Bucket = bucket,
                    Key = item.Key,
                    Size = (long)item.Size,
                    ContentType = string.Empty,
                    ETag = item.ETag,
                    LastModified = item.LastModifiedDateTime ?? DateTime.MinValue,
                    Metadata = new Dictionary<string, string>()
                });
            }
        }

        _logger.LogInformation("Listed {Count} objects in bucket {Bucket} with prefix '{Prefix}'", results.Count, bucket, prefix);

        return results.AsReadOnly();
    }

    public async Task<string> GetPresignedUrlAsync(
        string bucket,
        string key,
        TimeSpan expiry,
        CancellationToken ct = default)
    {
        var presignedArgs = new PresignedGetObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)
            .WithExpiry((int)expiry.TotalSeconds);

        var url = await _client.PresignedGetObjectAsync(presignedArgs);

        _logger.LogInformation("Generated presigned URL for {Key} in bucket {Bucket} (expires in {Expiry})", key, bucket, expiry);

        return url;
    }

    public async Task EnsureBucketExistsAsync(
        string bucket,
        CancellationToken ct = default)
    {
        var existsArgs = new BucketExistsArgs()
            .WithBucket(bucket);

        var exists = await _client.BucketExistsAsync(existsArgs, ct);

        if (!exists)
        {
            var makeArgs = new MakeBucketArgs()
                .WithBucket(bucket);

            await _client.MakeBucketAsync(makeArgs, ct);

            _logger.LogInformation("Created bucket {Bucket}", bucket);
        }
    }
}
