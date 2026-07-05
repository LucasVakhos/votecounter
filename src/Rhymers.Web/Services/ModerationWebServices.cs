using Rhymers.Core.Models;
using Rhymers.Core.Services;

namespace Rhymers.Web.Services;

/// <summary>
/// Blazor-обёртка для сервиса журнала аудита
/// </summary>
public sealed class AuditLogWebService
{
    private readonly AuditLogService _service;

    public AuditLogWebService(AuditLogService service)
    {
        _service = service;
    }

    public async Task<List<AuditLog>> GetLogsAsync(
        AuditAction? action = null,
        string? actorName = null,
        string? targetUserName = null,
        string? contestId = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 50)
        => await _service.GetLogsAsync(action, actorName, targetUserName, contestId, from, to, page, pageSize);

    public async Task<int> GetLogsCountAsync(
        AuditAction? action = null,
        string? actorName = null,
        string? targetUserName = null,
        string? contestId = null,
        DateTime? from = null,
        DateTime? to = null)
        => await _service.GetLogsCountAsync(action, actorName, targetUserName, contestId, from, to);
}

/// <summary>
/// Blazor-обёртка для сервиса уведомлений
/// </summary>
public sealed class NotificationWebService
{
    private readonly NotificationService _service;

    public NotificationWebService(NotificationService service)
    {
        _service = service;
    }

    public async Task<List<UserNotification>> GetNotificationsAsync(string userName, bool unreadOnly = false)
        => await _service.GetNotificationsAsync(userName, unreadOnly);

    public async Task<int> GetUnreadCountAsync(string userName)
        => await _service.GetUnreadCountAsync(userName);

    public async Task MarkReadAsync(string notificationId)
        => await _service.MarkReadAsync(notificationId);

    public async Task MarkAllReadAsync(string userName)
        => await _service.MarkAllReadAsync(userName);
}

/// <summary>
/// Blazor-обёртка для сервиса жалоб
/// </summary>
public sealed class AppealWebService
{
    private readonly AppealService _service;

    public AppealWebService(AppealService service)
    {
        _service = service;
    }

    public async Task<SanctionAppeal> SubmitAppealAsync(string violationId, string contestId, string userName, string reason)
        => await _service.SubmitAppealAsync(violationId, contestId, userName, reason);

    public async Task<List<SanctionAppeal>> GetAppealsAsync(AppealStatus? status = null, string? contestId = null)
        => await _service.GetAppealsAsync(status, contestId);

    public async Task<List<SanctionAppeal>> GetUserAppealsAsync(string userName)
        => await _service.GetUserAppealsAsync(userName);

    public async Task<bool> HasPendingAppealAsync(string violationId, string userName)
        => await _service.HasPendingAppealAsync(violationId, userName);

    public async Task<SanctionAppeal> AcceptAppealAsync(string appealId, string adminName, UserRole adminRole, string? comment = null)
        => await _service.AcceptAppealAsync(appealId, adminName, adminRole, comment);

    public async Task<SanctionAppeal> RejectAppealAsync(string appealId, string adminName, UserRole adminRole, string? comment = null)
        => await _service.RejectAppealAsync(appealId, adminName, adminRole, comment);
}
