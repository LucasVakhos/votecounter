namespace Rhymers.Core.Models;

/// <summary>
/// Типы нарушений в чате (хамство/троллинг, но НЕ спор)
/// </summary>
public enum ViolationType
{
    /// <summary>Грубость, мат, оскорбления личности</summary>
    Rudeness = 0,

    /// <summary>Троллинг, провокации, намеренные оскорбления</summary>
    Trolling = 1,

    /// <summary>Спам, флуд, дублирование</summary>
    Spam = 2,

    /// <summary>Другое нарушение</summary>
    Other = 3
}

/// <summary>
/// Запись о нарушении пользователя (хамство, грубость в чате)
/// </summary>
public sealed class UserViolation
{
    /// <summary>Уникальный идентификатор нарушения</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>ID контеста</summary>
    public string ContestId { get; set; } = string.Empty;

    /// <summary>Имя пользователя, совершившего нарушение</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>ID сообщения, которое было отмечено как хамство</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Тип нарушения</summary>
    public ViolationType Type { get; set; } = ViolationType.Rudeness;

    /// <summary>Дополнительная причина (комментарий модератора)</summary>
    public string? Details { get; set; }

    /// <summary>Имя модератора, отметившего нарушение</summary>
    public string ModeratorName { get; set; } = string.Empty;

    /// <summary>Дата нарушения</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Снято ли предупреждение (реабилитация)</summary>
    public bool IsCleared { get; set; } = false;

    /// <summary>Дата снятия предупреждения</summary>
    public DateTime? ClearedAt { get; set; }

    /// <summary>Имя модератора, снявшего предупреждение</summary>
    public string? ClearedByModerator { get; set; }
}

/// <summary>
/// Статистика нарушений пользователя
/// </summary>
public class UserViolationStats
{
    public string UserName { get; set; } = string.Empty;
    public int TotalViolations { get; set; }
    public int ActiveViolations { get; set; }
    public DateTime? LastViolationAt { get; set; }
    public bool IsBlocked { get; set; }
    public string? BlockReason { get; set; }
    public Dictionary<ViolationType, int> ViolationsByType { get; set; } = new();
}
