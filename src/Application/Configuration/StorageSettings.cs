using Microsoft.Extensions.Configuration;
using Storage.Domain.Enums;

namespace Storage.Application.Configuration;

public class MinioSettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = false;
}

// Garage is an open-source, geo-distributed S3-compatible object store (https://garagehq.deuxfleurs.fr).
// It exposes an S3-compatible API, so the MinIO SDK is used to communicate with it.
public class GarageSettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = false;
    // Garage requires an explicit region (default: "garage").
    public string Region { get; set; } = "garage";
    // Garage Admin API endpoint (default port 3901) / used for bucket creation and key permission grants.
    public string AdminEndpoint { get; set; } = string.Empty;
    public string AdminToken { get; set; } = string.Empty;
}

public class SeaweedFsSettings
{
    public string MasterUrl { get; set; } = string.Empty;
    public string FilerUrl { get; set; } = string.Empty;
}

public class AzureBlobSettings
{
    public string ConnectionString { get; set; } = string.Empty;
}

public class StorageSettings
{
    public StorageProviderType Provider { get; set; } = StorageProviderType.MinIO;
    public MinioSettings MinIO { get; set; } = new();
    public GarageSettings Garage { get; set; } = new();
    public SeaweedFsSettings SeaweedFS { get; set; } = new();
    public AzureBlobSettings AzureBlob { get; set; } = new();

    /// Binds StorageSettings from flat environment variables.
    public static StorageSettings BindFromConfiguration(IConfiguration configuration)
    {
        var settings = new StorageSettings();

        // Provider
        var providerStr = configuration["STORAGE_PROVIDER"]
            ?? throw new ArgumentNullException(nameof(configuration), "STORAGE_PROVIDER is not set.");

        if (!Enum.TryParse<StorageProviderType>(providerStr, true, out var provider))
            throw new ArgumentException($"Invalid STORAGE_PROVIDER value: '{providerStr}'. Expected: MinIO, SeaweedFS, AzureBlob, or Garage.");

        settings.Provider = provider;

        // Validate and bind provider-specific settings
        switch (provider)
        {
            case StorageProviderType.MinIO:
                settings.MinIO = BindMinioSettings(configuration);
                break;

            case StorageProviderType.SeaweedFS:
                settings.SeaweedFS = BindSeaweedFsSettings(configuration);
                break;

            case StorageProviderType.AzureBlob:
                settings.AzureBlob = BindAzureBlobSettings(configuration);
                break;

            case StorageProviderType.Garage:
                settings.Garage = BindGarageSettings(configuration);
                break;
        }

        return settings;
    }

    private static MinioSettings BindMinioSettings(IConfiguration configuration)
    {
        return new MinioSettings
        {
            Endpoint = configuration["MINIO_ENDPOINT"]
                ?? throw new ArgumentNullException(nameof(configuration), "MINIO_ENDPOINT is not set."),

            AccessKey = configuration["MINIO_ACCESS_KEY"]
                ?? throw new ArgumentNullException(nameof(configuration), "MINIO_ACCESS_KEY is not set."),

            SecretKey = configuration["MINIO_SECRET_KEY"]
                ?? throw new ArgumentNullException(nameof(configuration), "MINIO_SECRET_KEY is not set."),

            UseSsl = bool.Parse(
                configuration["MINIO_USE_SSL"]
                ?? throw new ArgumentNullException(nameof(configuration), "MINIO_USE_SSL is not set."))
        };
    }

    private static SeaweedFsSettings BindSeaweedFsSettings(IConfiguration configuration)
    {
        return new SeaweedFsSettings
        {
            MasterUrl = configuration["SEAWEEDFS_MASTER_URL"]
                ?? throw new ArgumentNullException(nameof(configuration), "SEAWEEDFS_MASTER_URL is not set."),

            FilerUrl = configuration["SEAWEEDFS_FILER_URL"]
                ?? throw new ArgumentNullException(nameof(configuration), "SEAWEEDFS_FILER_URL is not set.")
        };
    }

    private static AzureBlobSettings BindAzureBlobSettings(IConfiguration configuration)
    {
        return new AzureBlobSettings
        {
            ConnectionString = configuration["AZURE_BLOB_CONNECTION_STRING"]
                ?? throw new ArgumentNullException(nameof(configuration), "AZURE_BLOB_CONNECTION_STRING is not set.")
        };
    }

    private static GarageSettings BindGarageSettings(IConfiguration configuration)
    {
        return new GarageSettings
        {
            Endpoint = configuration["GARAGE_ENDPOINT"]
                ?? throw new ArgumentNullException(nameof(configuration), "GARAGE_ENDPOINT is not set."),

            AccessKey = configuration["GARAGE_ACCESS_KEY"]
                ?? throw new ArgumentNullException(nameof(configuration), "GARAGE_ACCESS_KEY is not set."),

            SecretKey = configuration["GARAGE_SECRET_KEY"]
                ?? throw new ArgumentNullException(nameof(configuration), "GARAGE_SECRET_KEY is not set."),

            UseSsl = bool.Parse(
                configuration["GARAGE_USE_SSL"]
                ?? throw new ArgumentNullException(nameof(configuration), "GARAGE_USE_SSL is not set.")),

            Region = configuration["GARAGE_REGION"] ?? "garage",

            AdminEndpoint = configuration["GARAGE_ADMIN_ENDPOINT"]
                ?? throw new ArgumentNullException(nameof(configuration), "GARAGE_ADMIN_ENDPOINT is not set."),

            AdminToken = configuration["GARAGE_ADMIN_TOKEN"]
                ?? throw new ArgumentNullException(nameof(configuration), "GARAGE_ADMIN_TOKEN is not set.")
        };
    }    
}