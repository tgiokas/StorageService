using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Storage.Application.Configuration;
using Storage.Application.Errors;
using Storage.Application.Interfaces;
using Storage.Domain.Enums;
using Storage.Domain.Interfaces;
using Storage.Infrastructure.Encryption;
using Storage.Infrastructure.Indexing;
using Storage.Infrastructure.Providers.MinIO;
using Storage.Infrastructure.Providers.SeaweedFS;
using Storage.Infrastructure.Providers.AzureBlob;
using Storage.Infrastructure.Providers.Garage;

namespace Storage.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind StorageSettings from env variables
        var settings = StorageSettings.BindFromConfiguration(configuration);
        services.AddSingleton(Options.Create(settings));

        // Bind EncryptionSettings
        var encryptionSettings = EncryptionSettings.BindFromConfiguration(configuration);
        services.AddSingleton(Options.Create(encryptionSettings));

        // Bind IndexingSettings
        var indexingSettings = IndexingSettings.BindFromConfiguration(configuration);
        services.AddSingleton(Options.Create(indexingSettings));

        // Register the concrete storage provider
        switch (settings.Provider)
        {
            case StorageProviderType.MinIO:
                services.AddSingleton<MinioStorageProvider>();
                break;

            case StorageProviderType.SeaweedFS:
                services.AddSingleton<SeaweedFsStorageProvider>();
                break;

            case StorageProviderType.AzureBlob:
                services.AddSingleton<AzureBlobStorageProvider>();
                break;

            case StorageProviderType.Garage:                
                services.AddSingleton<GarageStorageProvider>();
                break;

            default:
                throw new InvalidOperationException($"Unsupported storage provider: {settings.Provider}");
        }

        // Register IStorageProvider — with or without encryption decorator
        if (encryptionSettings.Enabled)
        {
            services.AddSingleton<IEncryptionService, AesGcmEncryptionService>();

            services.AddSingleton<IStorageProvider>(sp =>
            {
                var innerProvider = ResolveInnerProvider(sp, settings.Provider);
                var encryptionService = sp.GetRequiredService<IEncryptionService>();
                var logger = sp.GetRequiredService<ILogger<EncryptedStorageProviderDecorator>>();

                return new EncryptedStorageProviderDecorator(innerProvider, encryptionService, logger);
            });
        }
        else
        {
            services.AddSingleton<IStorageProvider>(sp => ResolveInnerProvider(sp, settings.Provider));
        }

        // Register document indexing — Elasticsearch
        if (indexingSettings.Enabled)
        {
            services.AddSingleton<IDocumentIndexRepository, ElasticDocumentIndexRepository>();
        }

        // Add Error Catalog Path
        var path = Path.Combine(Environment.CurrentDirectory, "errors.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"errors.json not found at: {path}");
        
        var errorcat = ErrorCatalog.LoadFromFile(path);
        services.AddSingleton<IErrorCatalog>(errorcat);

        return services;
    }

    private static IStorageProvider ResolveInnerProvider(IServiceProvider sp, StorageProviderType provider)
    {
        return provider switch
        {
            StorageProviderType.MinIO => sp.GetRequiredService<MinioStorageProvider>(),
            StorageProviderType.SeaweedFS => sp.GetRequiredService<SeaweedFsStorageProvider>(),
            StorageProviderType.AzureBlob => sp.GetRequiredService<AzureBlobStorageProvider>(),
            StorageProviderType.Garage => sp.GetRequiredService<GarageStorageProvider>(),
            _ => throw new InvalidOperationException($"Unsupported storage provider: {provider}")
        };
    }
}
