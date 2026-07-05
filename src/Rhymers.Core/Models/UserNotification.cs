namespace Rhymers.Core.Models;

/// <summary>
/// Типы уведомлений для пользователей
/// </summary>
public enum NotificationType
{
    /// <summary>Нарушение зафиксировано</summary>
    ViolationMarked = 0,

    /// <summary>Санкция применена</summary>
    SanctionApplied = 1,

    /// <summary>Санкция истекла</summary>
    SanctionExpired = 2,

    /// <summary>Санкция снята досрочно</summary>
    SanctionRemoved = 3,

    /// <summary>Нарушение снято</summary>
    ViolationCleared = 4,

    /// <summary>Жалоба принята</summary>
    AppealAccepted = 5,

    /// <summary>Жалоба отклонена</summary>
    AppealRejected = 6,

    /// <summary>Системное сообщение</summary>
    System = 7
}

/// <summary>
/// Уведомление для пользователя о санкциях и нарушениях
/// </summary>
public sealed class UserNotification
{
    /// <summary>Уникальный идентификатор уведомления</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Имя пользователя-получателя</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Тип уведомления</summary>
    public NotificationType Type { get; set; }

    /// <summary>Заголовок уведомления</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Текст уведомления</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Прочитано ли уведомление</summary>
    public bool IsRead { get; set; } = false;

    /// <summary>Дата создания</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Дата прочтения</summary>
    public DateTime? ReadAt { get; set; }
}
