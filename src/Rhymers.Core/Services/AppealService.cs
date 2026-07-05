using Rhymers.Core.Data;
using Rhymers.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Rhymers.Core.Services;

/// <summary>
/// Сервис для работы с жалобами пользователей на санкции
/// </summary>
public sealed class AppealService
{
    private readonly RhymersDbContext _context;
    private readonly NotificationService _notificationService;
    private readonly AuditLogService _auditLogService;

    public AppealService(
        RhymersDbContext context,
        NotificationService notificationService,
        AuditLogService auditLogService)
    {
        _context = context;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// Подать жалобу на санкцию
    /// </summary>
    public async Task<SanctionAppeal> SubmitAppealAsync(string violationId, string contestId, string userName, string reason)
    {
        // Проверка: нет ли уже активной жалобы на это нарушение
        var existing = await _context.SanctionAppeals
            .FirstOrDefaultAsync(a => a.ViolationId == violationId && a.UserName == userName && a.Status == AppealStatus.Pending);

        if (existing != null)
            throw new InvalidOperationException("Жалоба на это нарушение уже подана и ожидает рассмотрения.");

        var appeal = new SanctionAppeal
        {
            Id = Guid.NewGuid().ToString("N"),
            ViolationId = violationId,
            ContestId = contestId,
            UserName = userName,
            Reason = reason,
            Status = AppealStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.SanctionAppeals.Add(appeal);
        await _context.SaveChangesAsync();
        return appeal;
    }

    /// <summary>
    /// Получить все жалобы для очереди администратора
    /// </summary>
    public async Task<List<SanctionAppeal>> GetAppealsAsync(AppealStatus? status = null, string? contestId = null)
    {
        var query = _context.SanctionAppeals.AsQueryable();

        if (status.HasValue)
            query = query.Where(a => a.Status == status);

        if (!string.IsNullOrEmpty(contestId))
            query = query.Where(a => a.ContestId == contestId);

        return await query.OrderBy(a => a.CreatedAt).ToListAsync();
    }

    /// <summary>
    /// Получить жалобы конкретного пользователя
    /// </summary>
    public async Task<List<SanctionAppeal>> GetUserAppealsAsync(string userName)
    {
        return await _context.SanctionAppeals
            .Where(a => a.UserName == userName)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Проверить, есть ли у пользователя активная жалоба на санкцию по нарушению
    /// </summary>
    public async Task<bool> HasPendingAppealAsync(string violationId, string userName)
    {
        return await _context.SanctionAppeals
            .AnyAsync(a => a.ViolationId == violationId && a.UserName == userName && a.Status == AppealStatus.Pending);
    }

    /// <summary>
    /// Принять жалобу — снять санкцию с пользователя
    /// </summary>
    public async Task<SanctionAppeal> AcceptAppealAsync(string appealId, string adminName, UserRole adminRole, string? comment = null)
    {
        var appeal = await _context.SanctionAppeals.FindAsync(appealId)
            ?? throw new InvalidOperationException("Жалоба не найдена.");

        if (appeal.Status != AppealStatus.Pending)
            throw new InvalidOperationException("Жалоба уже рассмотрена.");

        // Снять санкцию с нарушения
        var violation = await _context.UserViolations.FindAsync(appeal.ViolationId);
        if (violation != null)
        {
            violation.Sanction = SanctionType.None;
            violation.SanctionExpiredAt = null;
            _context.UserViolations.Update(violation);
        }

        // Обновить статус жалобы
        appeal.Status = AppealStatus.Accepted;
        appeal.ReviewedAt = DateTime.UtcNow;
        appeal.ReviewedByAdmin = "🎭 Администрация";
        appeal.AdminComment = comment;
        _context.SanctionAppeals.Update(appeal);

        await _context.SaveChangesAsync();

        // Уведомить пользователя
        await _notificationService.NotifyAppealResultAsync(appeal.UserName, AppealStatus.Accepted, comment);

        // Аудит лог
        await _auditLogService.LogAsync(
            AuditAction.AppealAccepted,
            adminName, adminRole,
            appeal.UserName,
            $"Жалоба принята. {(string.IsNullOrEmpty(comment) ? "" : $"Комментарий: {comment}")}",
            appeal.ContestId, appeal.Id);

        return appeal;
    }

    /// <summary>
    /// Отклонить жалобу — оставить санкцию в силе
    /// </summary>
    public async Task<SanctionAppeal> RejectAppealAsync(string appealId, string adminName, UserRole adminRole, string? comment = null)
    {
        var appeal = await _context.SanctionAppeals.FindAsync(appealId)
            ?? throw new InvalidOperationException("Жалоба не найдена.");

        if (appeal.Status != AppealStatus.Pending)
            throw new InvalidOperationException("Жалоба уже рассмотрена.");

        appeal.Status = AppealStatus.Rejected;
        appeal.ReviewedAt = DateTime.UtcNow;
        appeal.ReviewedByAdmin = "🎭 Администрация";
        appeal.AdminComment = comment;
        _context.SanctionAppeals.Update(appeal);

        await _context.SaveChangesAsync();

        // Уведомить пользователя
        await _notificationService.NotifyAppealResultAsync(appeal.UserName, AppealStatus.Rejected, comment);

        // Аудит лог
        await _auditLogService.LogAsync(
            AuditAction.AppealRejected,
            adminName, adminRole,
            appeal.UserName,
            $"Жалоба отклонена. {(string.IsNullOrEmpty(comment) ? "" : $"Причина: {comment}")}",
            appeal.ContestId, appeal.Id);

        return appeal;
    }
}
