using ControlSystem.BL.Ai;
using ControlSystem.BL.Controls;
using ControlSystem.BL.Execution;
using ControlSystem.BL.Querying;
using ControlSystem.DAL.Ai;
using ControlSystem.DAL.Demo;
using ControlSystem.DAL.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ControlSystem.DAL;

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
