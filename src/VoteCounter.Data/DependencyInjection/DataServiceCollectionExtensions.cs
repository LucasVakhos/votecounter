using Microsoft.Extensions.DependencyInjection;
using VoteCounter.Data.Database;

namespace VoteCounter.Data.DependencyInjection;

/// <summary>
/// Extension methods for registering VoteCounter.Data services in DI container
/// </summary>
public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Register all data access services (database, stores, importers)
    /// </summary>
    public static IServiceCollection AddVoteCounterData(this IServiceCollection services)
    {
        // Store services
        services.AddSingleton<LocalStore>();
        services.AddSingleton<RhymeMachineStore>();
        services.AddSingleton<VoteImportReportService>();

        // Import services
        services.AddSingleton<FirebirdLegacyImporter>();

        return services;
    }

    /// <summary>
    /// Register local store service
    /// </summary>
    public static IServiceCollection AddLocalStore(this IServiceCollection services)
    {
        return services.AddSingleton<LocalStore>();
    }

    /// <summary>
    /// Register Firebird legacy importer
    /// </summary>
    public static IServiceCollection AddFirebirdLegacyImporter(this IServiceCollection services)
    {
        return services.AddSingleton<FirebirdLegacyImporter>();
    }
}
