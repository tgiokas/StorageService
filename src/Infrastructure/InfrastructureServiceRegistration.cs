using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StorageService.Application.Errors;
using StorageService.Application.Interfaces;
using StorageService.Domain.Enums;
using StorageService.Domain.Interfaces;
using StorageService.Infrastructure.Configuration;
using StorageService.Infrastructure.Encryption;
using StorageService.Infrastructure.Providers.MinIO;
using StorageService.Infrastructure.Providers.SeaweedFS;
using StorageService.Infrastructure.Providers.AzureBlob;

namespace StorageService.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddStorageInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind from flat env variables (with appsettings.json as fallback)
        var settings = StorageSettings.BindFromConfiguration(configuration);

        // Register as singleton so IOptions<StorageSettings> works everywhere
        services.AddSingleton(Options.Create(settings));

        // Bind encryption settings
        var encryptionSettings = EncryptionSettings.BindFromConfiguration(configuration);
        services.AddSingleton(Options.Create(encryptionSettings));

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

        // Error catalog
        var errorsPath = configuration["ErrorCatalogPath"] ?? "errors.json";
        var errorCatalog = ErrorCatalog.LoadFromFile(errorsPath);
        services.AddSingleton<IErrorCatalog>(errorCatalog);

        return services;
    }

    private static IStorageProvider ResolveInnerProvider(IServiceProvider sp, StorageProviderType provider)
    {
        return provider switch
        {
            StorageProviderType.MinIO => sp.GetRequiredService<MinioStorageProvider>(),
            StorageProviderType.SeaweedFS => sp.GetRequiredService<SeaweedFsStorageProvider>(),
            StorageProviderType.AzureBlob => sp.GetRequiredService<AzureBlobStorageProvider>(),
            _ => throw new InvalidOperationException($"Unsupported storage provider: {provider}")
        };
    }
}
