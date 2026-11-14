using ControlSystem.BL.Ai;
using ControlSystem.BL.Controls;
using ControlSystem.BL.Execution;
using ControlSystem.DAL.Ai;
using ControlSystem.DAL.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ControlSystem.DAL;

/// <summary>רישום המימושים של שכבת הנתונים בלבד (ממשקים שמוגדרים ב-BL). לוגיקה עסקית נרשמת ב-Program של ה-Api.</summary>
public static class DataAccessRegistration
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, DatabaseOptions db, AiOptions ai)
    {
        services.AddMemoryCache();
        services.AddSingleton(db);

        services.AddSingleton<ICatalogProvider, SqlCatalogProvider>();
        services.AddSingleton<IControlRepository, SqlControlRepository>();
        services.AddSingleton<IQueryExecutor, SqlQueryExecutor>();

        services.AddHttpClient<IAiControlProvider, HttpAiControlProvider>(c =>
        {
            c.BaseAddress = new Uri(ai.BaseUrl.TrimEnd('/') + "/");
            c.Timeout = TimeSpan.FromSeconds(ai.TimeoutSeconds);
        });
        return services;
    }
}
