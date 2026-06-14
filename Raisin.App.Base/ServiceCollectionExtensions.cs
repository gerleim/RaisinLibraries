using Microsoft.Extensions.DependencyInjection;

namespace Raisin.App.Base;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppEnvironment(this IServiceCollection services, string appName)
    {
        services.AddSingleton<IAppEnvironment>(new AppEnvironment(appName));
        return services;
    }
}
