namespace Rhymers.Core.Models;

/// <summary>
/// Статус жалобы на санкцию
/// </summary>
public enum AppealStatus
{
    /// <summary>Ожидает рассмотрения</summary>
    Pending = 0,

    /// <summary>Принята — санкция снята</summary>
    Accepted = 1,

    /// <summary>Отклонена — санкция сохранена</summary>
    Rejected = 2
}

/// <summary>
/// Жалоба пользователя на применённую санкцию
/// </summary>
public sealed class SanctionAppeal
{
    /// <summary>Уникальный идентификатор жалобы</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>ID нарушения, к которому привязана санкция</summary>
    public string ViolationId { get; set; } = string.Empty;

    /// <summary>ID контеста</summary>
    public string ContestId { get; set; } = string.Empty;

    /// <summary>Имя пользователя, подавшего жалобу</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Текст жалобы</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Статус жалобы</summary>
    public AppealStatus Status { get; set; } = AppealStatus.Pending;

    /// <summary>Дата подачи жалобы</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Дата рассмотрения жалобы</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Имя администратора, рассмотревшего жалобу</summary>
    public string? ReviewedByAdmin { get; set; }

    /// <summary>Комментарий администратора</summary>
    public string? AdminComment { get; set; }
}
