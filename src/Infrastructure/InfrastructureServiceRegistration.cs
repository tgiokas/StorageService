using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using StorageService.Application.Errors;
using StorageService.Application.Interfaces;
using StorageService.Domain.Enums;
using StorageService.Domain.Interfaces;
using StorageService.Infrastructure.Configuration;
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
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(settings));

        switch (settings.Provider)
        {
            case StorageProviderType.MinIO:
                services.AddSingleton<IStorageProvider, MinioStorageProvider>();
                break;

            case StorageProviderType.SeaweedFS:
                services.AddSingleton<IStorageProvider, SeaweedFsStorageProvider>();
                break;

            case StorageProviderType.AzureBlob:
                services.AddSingleton<IStorageProvider, AzureBlobStorageProvider>();
                break;

            default:
                throw new InvalidOperationException($"Unsupported storage provider: {settings.Provider}");
        }

        //// Error catalog
        //var errorsPath = configuration["ErrorCatalogPath"] ?? "errors.json";
        //var errorCatalog = ErrorCatalog.LoadFromFile(errorsPath);
        //services.AddSingleton<IErrorCatalog>(errorCatalog);

        return services;
    }
}
