using Microsoft.Extensions.DependencyInjection;

using Storage.Application.Interfaces;
using Storage.Application.Services;

namespace Storage.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IDocumentStorageService, DocumentStorageService>();
        services.AddScoped<IDocumentIndexService, DocumentIndexService>();

        return services;
    }
}
