using ControlSystem.Core.Ai;
using ControlSystem.Core.Controls;
using ControlSystem.Core.Execution;
using ControlSystem.Core.Querying;
using ControlSystem.Infrastructure.Ai;
using ControlSystem.Infrastructure.Demo;
using ControlSystem.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ControlSystem.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddControlSystem(this IServiceCollection services, DatabaseOptions db,     AiOptions ai, bool demoMode = false)
    {
        services.AddMemoryCache();
        services.AddSingleton(db);
            if (demoMode)
            {
                services.AddSingleton<ICatalogProvider, DemoCatalogProvider>();
                services.AddSingleton<IControlRepository, DemoControlRepository>();
                services.AddSingleton<IQueryExecutor, DemoQueryExecutor>();
            }
            else
            {
                services.AddSingleton<ICatalogProvider, SqlCatalogProvider>();
                services.AddSingleton<IControlRepository, SqlControlRepository>();
                services.AddSingleton<IQueryExecutor, SqlQueryExecutor>();
            }
        services.AddSingleton<IQueryBuilder, SqlServerQueryBuilder>();
        services.AddSingleton<ControlService>();
        services.AddSingleton<ControlRunner>();
        services.AddTransient<AiControlService>();

        services.AddHttpClient<IAiControlProvider, HttpAiControlProvider>(c =>
        {
            c.BaseAddress = new Uri(ai.BaseUrl.TrimEnd('/') + "/");
            c.Timeout = TimeSpan.FromSeconds(ai.TimeoutSeconds);
        });
        return services;
    }
}
