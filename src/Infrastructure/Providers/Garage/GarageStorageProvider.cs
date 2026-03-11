using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Storage.Domain.Interfaces;
using Storage.Domain.Exceptions;
using Storage.Domain.ValueObjects;
using Storage.Application.Configuration;

namespace Storage.Infrastructure.Providers.Garage;

// Garage (https://garagehq.deuxfleurs.fr) is an open-source, geo-distributed S3-compatible
// object store written in Rust. It exposes a standard S3 API, so the MinIO SDK is used to
// communicate with it — the key differences from MinIO are:
//   - An explicit region must be set (any non-empty string; conventionally "garage")
//   - Garage does not support object locking or some advanced S3 features
//   - Bucket creation must go through the Admin API (port 3901), not the S3 API
//   - After creating a bucket via Admin API the key must be explicitly granted read/write access
public class GarageStorageProvider : IStorageProvider
{
    private readonly IMinioClient _client;
    private readonly HttpClient _adminHttp;
    private readonly GarageSettings _settings;
    private readonly ILogger<GarageStorageProvider> _logger;

    public GarageStorageProvider(IOptions<StorageSettings> options, ILogger<GarageStorageProvider> logger)
    {
        _logger = logger;
        _settings = options.Value.Garage;

        // ── S3 client (MinIO SDK) ────────────────────────────────────────────
        var builder = new MinioClient()
            .WithEndpoint(_settings.Endpoint)
            .WithCredentials(_settings.AccessKey, _settings.SecretKey)
            .WithRegion(_settings.Region);

        if (_settings.UseSsl)
            builder = builder.WithSSL();

        _client = builder.Build();

        // ── Admin HTTP client ────────────────────────────────────────────────
        _adminHttp = new HttpClient { BaseAddress = new Uri(_settings.AdminEndpoint) };
        _adminHttp.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.AdminToken);

        _logger.LogInformation(
            "Garage storage provider initialized — S3: {Endpoint}, Admin: {AdminEndpoint}, Region: {Region}",
            _settings.Endpoint, _settings.AdminEndpoint, _settings.Region);
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

        var putArgs = new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(key)
            .WithStreamData(content)
            .WithObjectSize(content.CanSeek ? content.Length : -1)
            .WithContentType(contentType);

        if (metadata != null && metadata.Count > 0)
            putArgs = putArgs.WithHeaders(metadata);

        var response = await _client.PutObjectAsync(putArgs, ct);

        _logger.LogInformation("Uploaded object {Key} to bucket {Bucket} ({Size} bytes)", key, bucket, originalSize);

        return new StorageObjectInfo
        {
            Bucket = bucket,
            Key = key,
            Size = originalSize,
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

        try
        {
            var getArgs = new GetObjectArgs()
                .WithBucket(bucket)
                .WithObject(key)
                .WithCallbackStream(async (stream, cancellationToken) =>
                {
                    await stream.CopyToAsync(memoryStream, cancellationToken);
                });

            await _client.GetObjectAsync(getArgs, ct);
        }
        catch (ObjectNotFoundException)
        {
            throw new StorageObjectNotFoundException(bucket, key);
        }
        catch (BucketNotFoundException)
        {
            throw new StorageObjectNotFoundException(bucket, key);
        }

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
        try
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
                    ? new Dictionary<string, string>(stat.MetaData)
                    : new Dictionary<string, string>()
            };
        }
        catch (ObjectNotFoundException)
        {
            throw new StorageObjectNotFoundException(bucket, key);
        }
        catch (BucketNotFoundException)
        {
            throw new StorageObjectNotFoundException(bucket, key);
        }
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
        catch (ObjectNotFoundException)
        {
            return false;
        }
        catch (BucketNotFoundException)
        {
            return false;
        }
    }

    public async Task EnsureBucketExistsAsync(
        string bucket,
        CancellationToken ct = default)
    {
        // CreateBucket
        // Returns 200 with full bucket info (including id) if created.
        // Returns 409 if the bucket already exists.      
        var createUrl = "/v2/CreateBucket";
        var createContent = new StringContent(
            JsonSerializer.Serialize(new { globalAlias = bucket }),
            Encoding.UTF8, "application/json");
        var createResponse = await _adminHttp.PostAsync(createUrl, createContent, ct);
        string bucketId;

        if (createResponse.IsSuccessStatusCode)
        {
            // Bucket just created — extract id from response body directly
            var json = await createResponse.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            bucketId = doc.RootElement.GetProperty("id").GetString()
                ?? throw new InvalidOperationException("Garage CreateBucket returned no 'id'.");

            _logger.LogInformation("Created Garage bucket '{Bucket}' with id {Id}", bucket, bucketId);
        }
        else if ((int)createResponse.StatusCode == 409)
        {
            // GetBucketInfo
            // Already exists — resolve id 
            var infoUrl = $"/v2/GetBucketInfo?globalAlias={Uri.EscapeDataString(bucket)}";
            var infoResponse = await _adminHttp.GetAsync(infoUrl, ct);

            if (!infoResponse.IsSuccessStatusCode)
            {
                var errorBody = await infoResponse.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"Garage GetBucketInfo failed: {infoResponse.StatusCode} — {errorBody}");
            }

            var infoJson = await infoResponse.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(infoJson);
            bucketId = doc.RootElement.GetProperty("id").GetString()
                ?? throw new InvalidOperationException("Garage GetBucketInfo returned no 'id'.");

            _logger.LogDebug("Resolved existing Garage bucket '{Bucket}' id: {Id}", bucket, bucketId);
        }
        else
        {
            var body = await createResponse.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Garage CreateBucket failed: {createResponse.StatusCode} — {body}");
        }

        // AllowBucketKey
        // Grants the configured key read + write access to the bucket. 
        var allowUrl = "/v2/AllowBucketKey";
        var allowContent = new StringContent(
            JsonSerializer.Serialize(new
            {
                bucketId,
                accessKeyId = _settings.AccessKey,
                permissions = new { read = true, write = true, owner = false }
            }),
            Encoding.UTF8, "application/json");

        var allowResponse = await _adminHttp.PostAsync(allowUrl, allowContent, ct);

        if (!allowResponse.IsSuccessStatusCode)
        {
            var body = await allowResponse.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Garage AllowBucketKey failed: {allowResponse.StatusCode} — {body}");
        }

        _logger.LogInformation("Key '{AccessKey}' has read/write access to Garage bucket '{Bucket}'", _settings.AccessKey, bucket);
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

        _logger.LogInformation("Generated presigned URL for {Key} in bucket {Bucket} (expires in {Expiry})",
            key, bucket, expiry);

        return url;
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

        if (!string.IsNullOrEmpty(prefix))
            listArgs = listArgs.WithPrefix(prefix);

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

        _logger.LogInformation("Listed {Count} objects in bucket {Bucket} with prefix '{Prefix}'",
            results.Count, bucket, prefix);

        return results.AsReadOnly();
    }

}