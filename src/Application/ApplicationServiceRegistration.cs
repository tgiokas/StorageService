using Microsoft.Extensions.DependencyInjection;

using StorageService.Application.Interfaces;
using StorageService.Application.Services;

namespace StorageService.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IDocumentStorageService, DocumentStorageService>();

        return services;
    }
}
