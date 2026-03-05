using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Storage.Domain.Enums;
using Storage.Domain.Interfaces;
using Storage.Infrastructure.Configuration;
using Storage.Infrastructure.Database;
using Storage.Infrastructure.Encryption;
using Storage.Infrastructure.Providers.MinIO;
using Storage.Infrastructure.Providers.SeaweedFS;
using Storage.Infrastructure.Providers.AzureBlob;
using Storage.Infrastructure.Repositories;

namespace Storage.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddStorageInfrastructure(this IServiceCollection services, IConfiguration configuration, string databaseProvider)
    {
        // Bind StorageSettings from env variables
        var settings = StorageSettings.BindFromConfiguration(configuration);

        // Register as singleton so IOptions<StorageSettings> works everywhere
        services.AddSingleton(Options.Create(settings));

        // Bind EncryptionSettings
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
        
        var connectionString = configuration.GetConnectionString("DefaultConnection");        
        //var connectionString = configuration["STORAGE_DB_CONNECTION"];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Database connection string 'STORAGE_DB_CONNECTION' is not configured.");

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            switch (databaseProvider.ToLower())
            {
                case "sqlserver":
                    //options.UseSqlServer(connectionString);
                    break;

                case "postgresql":
                    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
                    break;

                case "sqlite":
                    //options.UseSqlite(connectionString);                     
                    break;

                default:
                    throw new ArgumentException($"Unsupported database provider: {databaseProvider}");
            }
        });

        services.AddScoped<IDocumentIndexRepository, DocumentIndexRepository>();

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
