using Microsoft.Extensions.DependencyInjection;

using Storage.Application.Interfaces;
using Storage.Application.Services;

namespace Storage.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, bool indexingEnabled)
    {
        services.AddSingleton<IDocumentStorageService, DocumentStorageService>();

        if (indexingEnabled)
        {
            services.AddSingleton<IDocumentIndexService, DocumentIndexService>();
        }

        return services;
    }
}
