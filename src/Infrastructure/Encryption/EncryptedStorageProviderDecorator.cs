using Microsoft.Extensions.Logging;

using StorageService.Domain.Interfaces;
using StorageService.Domain.ValueObjects;

namespace StorageService.Infrastructure.Encryption;

/// A Decorator that wraps any IStorageProvider with encryption.
///
/// On upload: encrypts the stream before passing to the inner provider.
/// On download: decrypts the stream after receiving from the inner provider.
/// All other operations pass through unchanged.
/// 
/// Encrypted objects are tagged with metadata key "x-encrypted" = "true"
/// so the system knows which objects need decryption on download.
public class EncryptedStorageProviderDecorator : IStorageProvider
{
    private readonly IStorageProvider _inner;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<EncryptedStorageProviderDecorator> _logger;

    private const string EncryptedMetadataKey = "x-encrypted";
    private const string EncryptedMetadataValue = "true";

    public EncryptedStorageProviderDecorator(
        IStorageProvider inner,
        IEncryptionService encryptionService,
        ILogger<EncryptedStorageProviderDecorator> logger)
    {
        _inner = inner;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<StorageObjectInfo> UploadAsync(
        string bucket,
        string key,
        Stream content,
        string contentType,
        Dictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        // Encrypt the content
        var encryptedStream = await _encryptionService.EncryptAsync(content, ct);

        // Tag metadata so we know this object is encrypted
        metadata ??= new Dictionary<string, string>();
        metadata[EncryptedMetadataKey] = EncryptedMetadataValue;

        _logger.LogInformation("Encrypting document {Key} before upload to bucket {Bucket}", key, bucket);

        var result = await _inner.UploadAsync(bucket, key, encryptedStream, contentType, metadata, ct);

        // Report the original (unencrypted) size in the returned info
        // The stored size is larger due to nonce + tag overhead
        result.Metadata = metadata;

        return result;
    }

    public async Task<Stream> DownloadAsync(
        string bucket,
        string key,
        CancellationToken ct = default)
    {
        var encryptedStream = await _inner.DownloadAsync(bucket, key, ct);

        // Check if the object was encrypted by reading metadata
        var metadata = await _inner.GetMetadataAsync(bucket, key, ct);
        var isEncrypted = metadata.Metadata.TryGetValue(EncryptedMetadataKey, out var val)
            && val.Equals(EncryptedMetadataValue, StringComparison.OrdinalIgnoreCase);

        if (!isEncrypted)
        {
            _logger.LogDebug("Document {Key} in bucket {Bucket} is not encrypted, returning as-is", key, bucket);
            return encryptedStream;
        }

        _logger.LogInformation("Decrypting document {Key} after download from bucket {Bucket}", key, bucket);

        return await _encryptionService.DecryptAsync(encryptedStream, ct);
    }

    // All other operations pass through to the inner provider unchanged

    public Task DeleteAsync(string bucket, string key, CancellationToken ct = default)
        => _inner.DeleteAsync(bucket, key, ct);

    public Task<StorageObjectInfo> GetMetadataAsync(string bucket, string key, CancellationToken ct = default)
        => _inner.GetMetadataAsync(bucket, key, ct);

    public Task<bool> ExistsAsync(string bucket, string key, CancellationToken ct = default)
        => _inner.ExistsAsync(bucket, key, ct);

    public Task<IReadOnlyList<StorageObjectInfo>> ListAsync(string bucket, string? prefix = null, CancellationToken ct = default)
        => _inner.ListAsync(bucket, prefix, ct);

    public Task<string> GetPresignedUrlAsync(string bucket, string key, TimeSpan expiry, CancellationToken ct = default)
        => _inner.GetPresignedUrlAsync(bucket, key, expiry, ct);

    public Task EnsureBucketExistsAsync(string bucket, CancellationToken ct = default)
        => _inner.EnsureBucketExistsAsync(bucket, ct);
}
