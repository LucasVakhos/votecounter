using Microsoft.Extensions.DependencyInjection;
using VoteCounter.Core.Services;

namespace VoteCounter.Core.DependencyInjection;

/// <summary>
/// Extension methods for registering VoteCounter.Core services in DI container
/// </summary>
public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Register all core services (parsing, validation, processing)
    /// </summary>
    public static IServiceCollection AddVoteCounterCore(this IServiceCollection services)
    {
        // Vote processing services
        services.AddSingleton<VoteParser>();
        services.AddSingleton<VoteRuleService>();
        services.AddSingleton<ContestResultsService>();
        services.AddSingleton<VoteAuditService>();

        // Moderation services
        services.AddSingleton<ModerationService>();
        services.AddSingleton<ContentModerationService>();

        // Authorization and role management
        services.AddSingleton<RoleAuthorizationService>();

        // Text processing services (sealed classes)
        services.AddSingleton<WorkTextImporter>();

        // Business logic services
        services.AddSingleton<ContestTextImporter>();
        services.AddSingleton<SingleWorkSubmissionImporter>();
        services.AddSingleton<PrivateMessageWorkImporter>();
        services.AddSingleton<AuthorDisclosureImporter>();

        // Report and export services
        services.AddSingleton<ContestReportExportService>();
        services.AddSingleton<ExcelResultBuilder>();

        // Validation and checking services
        services.AddSingleton<ContestRulesAutoFixService>();
        services.AddSingleton<WorkSpellChecker>();

        return services;
    }

    /// <summary>
    /// Register a specific service if needed individually
    /// </summary>
    public static IServiceCollection AddVoteParser(this IServiceCollection services)
    {
        return services.AddSingleton<VoteParser>();
    }

    /// <summary>
    /// Register vote rule service
    /// </summary>
    public static IServiceCollection AddVoteRuleService(this IServiceCollection services)
    {
        return services.AddSingleton<VoteRuleService>();
    }

    /// <summary>
    /// Register results service
    /// </summary>
    public static IServiceCollection AddContestResultsService(this IServiceCollection services)
    {
        return services.AddSingleton<ContestResultsService>();
    }
}
