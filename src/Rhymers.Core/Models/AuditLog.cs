namespace Rhymers.Core.Models;

/// <summary>
/// Типы действий в журнале аудита
/// </summary>
public enum AuditAction
{
    /// <summary>Нарушение зафиксировано модератором</summary>
    ViolationMarked = 0,

    /// <summary>Нарушение снято</summary>
    ViolationCleared = 1,

    /// <summary>Санкция применена</summary>
    SanctionApplied = 2,

    /// <summary>Санкция снята</summary>
    SanctionRemoved = 3,

    /// <summary>Роль пользователя изменена</summary>
    RoleChanged = 4,

    /// <summary>Жалоба принята</summary>
    AppealAccepted = 5,

    /// <summary>Жалоба отклонена</summary>
    AppealRejected = 6,

    /// <summary>Санкция истекла автоматически</summary>
    SanctionExpired = 7,

    /// <summary>Автоматическая санкция применена по порогу</summary>
    AutoSanctionApplied = 8
}

/// <summary>
/// Запись в журнале аудита действий администрации
/// </summary>
public sealed class AuditLog
{
    /// <summary>Уникальный идентификатор записи</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Тип действия</summary>
    public AuditAction Action { get; set; }

    /// <summary>Имя администратора/модератора, выполнившего действие</summary>
    public string ActorName { get; set; } = string.Empty;

    /// <summary>Роль актора</summary>
    public UserRole ActorRole { get; set; }

    /// <summary>Имя пользователя, в отношении которого выполнено действие</summary>
    public string TargetUserName { get; set; } = string.Empty;

    /// <summary>ID контеста (если применимо)</summary>
    public string? ContestId { get; set; }

    /// <summary>ID связанного объекта (violation/appeal/user)</summary>
    public string? RelatedEntityId { get; set; }

    /// <summary>Детальное описание действия</summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>Дата и время действия</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
