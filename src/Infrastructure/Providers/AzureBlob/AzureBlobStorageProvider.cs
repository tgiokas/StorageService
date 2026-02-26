using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StorageService.Domain.Interfaces;
using StorageService.Domain.ValueObjects;
using StorageService.Infrastructure.Configuration;

namespace StorageService.Infrastructure.Providers.AzureBlob;

public class AzureBlobStorageProvider : IStorageProvider
{
    private readonly AzureBlobSettings _settings;
    private readonly ILogger<AzureBlobStorageProvider> _logger;

    public AzureBlobStorageProvider(IOptions<StorageSettings> options, ILogger<AzureBlobStorageProvider> logger)
    {
        _settings = options.Value.AzureBlob;
        _logger = logger;
        _logger.LogInformation("Azure Blob storage provider initialized");
    }

    public Task<StorageObjectInfo> UploadAsync(string bucket, string key, Stream content, string contentType, Dictionary<string, string>? metadata = null, CancellationToken ct = default)
    {
        throw new NotImplementedException("Azure Blob adapter will be implemented in Step 4.");
    }

    public Task<Stream> DownloadAsync(string bucket, string key, CancellationToken ct = default)
    {
        throw new NotImplementedException("Azure Blob adapter will be implemented in Step 4.");
    }

    public Task DeleteAsync(string bucket, string key, CancellationToken ct = default)
    {
        throw new NotImplementedException("Azure Blob adapter will be implemented in Step 4.");
    }

    public Task<StorageObjectInfo> GetMetadataAsync(string bucket, string key, CancellationToken ct = default)
    {
        throw new NotImplementedException("Azure Blob adapter will be implemented in Step 4.");
    }

    public Task<bool> ExistsAsync(string bucket, string key, CancellationToken ct = default)
    {
        throw new NotImplementedException("Azure Blob adapter will be implemented in Step 4.");
    }

    public Task<IReadOnlyList<StorageObjectInfo>> ListAsync(string bucket, string? prefix = null, CancellationToken ct = default)
    {
        throw new NotImplementedException("Azure Blob adapter will be implemented in Step 4.");
    }

    public Task<string> GetPresignedUrlAsync(string bucket, string key, TimeSpan expiry, CancellationToken ct = default)
    {
        throw new NotImplementedException("Azure Blob adapter will be implemented in Step 4.");
    }

    public Task EnsureBucketExistsAsync(string bucket, CancellationToken ct = default)
    {
        throw new NotImplementedException("Azure Blob adapter will be implemented in Step 4.");
    }
}
