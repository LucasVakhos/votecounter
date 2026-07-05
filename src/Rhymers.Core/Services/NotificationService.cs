using Rhymers.Core.Data;
using Rhymers.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Rhymers.Core.Services;

/// <summary>
/// Сервис уведомлений пользователей о санкциях, нарушениях и жалобах
/// </summary>
public sealed class NotificationService
{
    private readonly RhymersDbContext _context;

    public NotificationService(RhymersDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Отправить уведомление пользователю
    /// </summary>
    public async Task<UserNotification> SendAsync(string userName, NotificationType type, string title, string message)
    {
        var notification = new UserNotification
        {
            Id = Guid.NewGuid().ToString("N"),
            UserName = userName,
            Type = type,
            Title = title,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.UserNotifications.Add(notification);
        await _context.SaveChangesAsync();
        return notification;
    }

    /// <summary>
    /// Уведомить о фиксации нарушения
    /// </summary>
    public async Task NotifyViolationMarkedAsync(string userName, ViolationType violationType, string? details = null)
    {
        var typeDisplay = violationType switch
        {
            ViolationType.Rudeness => "грубость/оскорбление",
            ViolationType.Trolling => "троллинг/провокации",
            ViolationType.Spam => "спам/флуд",
            ViolationType.Other => "нарушение правил",
            _ => "нарушение"
        };

        var msg = $"В чате зафиксировано ваше нарушение: {typeDisplay}.";
        if (!string.IsNullOrEmpty(details))
            msg += $" Причина: {details}";

        await SendAsync(userName, NotificationType.ViolationMarked,
            "⚠️ Зафиксировано нарушение", msg);
    }

    /// <summary>
    /// Уведомить о применении санкции
    /// </summary>
    public async Task NotifySanctionAppliedAsync(string userName, SanctionType sanction, string? reason = null, DateTime? expiredAt = null)
    {
        var sanctionDisplay = sanction switch
        {
            SanctionType.OneDay => "на 1 день",
            SanctionType.OneWeek => "на 7 дней",
            SanctionType.OneMonth => "на 30 дней",
            SanctionType.Permanent => "навсегда",
            _ => "временно"
        };

        var msg = $"Вам применена санкция — ограничение доступа к чату {sanctionDisplay}.";
        if (!string.IsNullOrEmpty(reason))
            msg += $" Причина: {reason}";
        if (expiredAt.HasValue)
            msg += $" Заблокировано до: {expiredAt.Value:dd.MM.yyyy HH:mm} UTC.";
        msg += " Вы можете подать жалобу, если считаете санкцию несправедливой.";

        await SendAsync(userName, NotificationType.SanctionApplied,
            "🚫 Применена санкция", msg);
    }

    /// <summary>
    /// Уведомить об истечении санкции
    /// </summary>
    public async Task NotifySanctionExpiredAsync(string userName)
    {
        await SendAsync(userName, NotificationType.SanctionExpired,
            "✅ Санкция истекла",
            "Ваша санкция истекла — вы снова можете писать в чате. Соблюдайте правила!");
    }

    /// <summary>
    /// Уведомить о досрочном снятии санкции
    /// </summary>
    public async Task NotifySanctionRemovedAsync(string userName, string? adminComment = null)
    {
        var msg = "Ваша санкция снята досрочно администрацией. Вы снова можете писать в чате.";
        if (!string.IsNullOrEmpty(adminComment))
            msg += $" Комментарий: {adminComment}";

        await SendAsync(userName, NotificationType.SanctionRemoved,
            "✅ Санкция снята", msg);
    }

    /// <summary>
    /// Уведомить о снятии нарушения
    /// </summary>
    public async Task NotifyViolationClearedAsync(string userName)
    {
        await SendAsync(userName, NotificationType.ViolationCleared,
            "✅ Нарушение снято",
            "Ранее зафиксированное нарушение снято администрацией. Ваша история нарушений обновлена.");
    }

    /// <summary>
    /// Уведомить о результате жалобы
    /// </summary>
    public async Task NotifyAppealResultAsync(string userName, AppealStatus status, string? adminComment = null)
    {
        if (status == AppealStatus.Accepted)
        {
            var msg = "Ваша жалоба на санкцию рассмотрена и принята — санкция снята!";
            if (!string.IsNullOrEmpty(adminComment))
                msg += $" Комментарий: {adminComment}";
            await SendAsync(userName, NotificationType.AppealAccepted, "✅ Жалоба принята", msg);
        }
        else if (status == AppealStatus.Rejected)
        {
            var msg = "Ваша жалоба на санкцию рассмотрена и отклонена — санкция сохранена.";
            if (!string.IsNullOrEmpty(adminComment))
                msg += $" Причина: {adminComment}";
            await SendAsync(userName, NotificationType.AppealRejected, "❌ Жалоба отклонена", msg);
        }
    }

    /// <summary>
    /// Получить все уведомления пользователя
    /// </summary>
    public async Task<List<UserNotification>> GetNotificationsAsync(string userName, bool unreadOnly = false)
    {
        var query = _context.UserNotifications.Where(n => n.UserName == userName);

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        return await query.OrderByDescending(n => n.CreatedAt).ToListAsync();
    }

    /// <summary>
    /// Количество непрочитанных уведомлений
    /// </summary>
    public async Task<int> GetUnreadCountAsync(string userName)
    {
        return await _context.UserNotifications
            .CountAsync(n => n.UserName == userName && !n.IsRead);
    }

    /// <summary>
    /// Отметить уведомление как прочитанное
    /// </summary>
    public async Task MarkReadAsync(string notificationId)
    {
        var notification = await _context.UserNotifications.FindAsync(notificationId);
        if (notification == null)
            return;

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        _context.UserNotifications.Update(notification);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Отметить все уведомления пользователя как прочитанные
    /// </summary>
    public async Task MarkAllReadAsync(string userName)
    {
        var unread = await _context.UserNotifications
            .Where(n => n.UserName == userName && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }
}
