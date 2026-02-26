using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StorageService.Domain.Interfaces;
using StorageService.Domain.ValueObjects;
using StorageService.Infrastructure.Configuration;

namespace StorageService.Infrastructure.Providers.SeaweedFS;

public class SeaweedFsStorageProvider : IStorageProvider
{
    private readonly SeaweedFsSettings _settings;
    private readonly ILogger<SeaweedFsStorageProvider> _logger;

    public SeaweedFsStorageProvider(IOptions<StorageSettings> options, ILogger<SeaweedFsStorageProvider> logger)
    {
        _settings = options.Value.SeaweedFS;
        _logger = logger;
        _logger.LogInformation("SeaweedFS storage provider initialized with filer: {FilerUrl}", _settings.FilerUrl);
    }

    public Task<StorageObjectInfo> UploadAsync(string bucket, string key, Stream content, string contentType, Dictionary<string, string>? metadata = null, CancellationToken ct = default)
    {
        throw new NotImplementedException("SeaweedFS adapter will be implemented in Step 4.");
    }

    public Task<Stream> DownloadAsync(string bucket, string key, CancellationToken ct = default)
    {
        throw new NotImplementedException("SeaweedFS adapter will be implemented in Step 4.");
    }

    public Task DeleteAsync(string bucket, string key, CancellationToken ct = default)
    {
        throw new NotImplementedException("SeaweedFS adapter will be implemented in Step 4.");
    }

    public Task<StorageObjectInfo> GetMetadataAsync(string bucket, string key, CancellationToken ct = default)
    {
        throw new NotImplementedException("SeaweedFS adapter will be implemented in Step 4.");
    }

    public Task<bool> ExistsAsync(string bucket, string key, CancellationToken ct = default)
    {
        throw new NotImplementedException("SeaweedFS adapter will be implemented in Step 4.");
    }

    public Task<IReadOnlyList<StorageObjectInfo>> ListAsync(string bucket, string? prefix = null, CancellationToken ct = default)
    {
        throw new NotImplementedException("SeaweedFS adapter will be implemented in Step 4.");
    }

    public Task<string> GetPresignedUrlAsync(string bucket, string key, TimeSpan expiry, CancellationToken ct = default)
    {
        throw new NotImplementedException("SeaweedFS adapter will be implemented in Step 4.");
    }

    public Task EnsureBucketExistsAsync(string bucket, CancellationToken ct = default)
    {
        throw new NotImplementedException("SeaweedFS adapter will be implemented in Step 4.");
    }
}
