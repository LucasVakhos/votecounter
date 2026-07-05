namespace Rhymers.Core.Models;

/// <summary>
/// Публичная рецензия на конкретную работу.
/// </summary>
/// <remarks>
/// Позволяет авторам, экспертам и зрителям оставлять развёрнутые рецензии
/// на работы после завершения конкурса. Рецензии модерируются перед публикацией.
/// </remarks>
public sealed class WorkReview
{
    /// <summary>Уникальный идентификатор рецензии</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>ID конкурса</summary>
    public string ContestId { get; set; } = string.Empty;

    /// <summary>Номер работы в рамках конкурса (1, 2, 3...)</summary>
    public int WorkNumber { get; set; }

    /// <summary>Заголовок работы (для удобства)</summary>
    public string WorkTitle { get; set; } = string.Empty;

    /// <summary>Автор рецензии</summary>
    public string ReviewerName { get; set; } = string.Empty;

    /// <summary>Роль автора рецензии (для фильтрации по авторитету)</summary>
    public UserRole ReviewerRole { get; set; } = UserRole.Reader;

    /// <summary>Заголовок рецензии</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Основной текст рецензии</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Оценка работы (1-5 звёзд)</summary>
    public int? Rating { get; set; }

    /// <summary>Основные достоинства (короткий список)</summary>
    public string? Strengths { get; set; }

    /// <summary>Области для улучшения</summary>
    public string? AreasForImprovement { get; set; }

    /// <summary>Одобрена ли рецензия модератором</summary>
    public bool IsApproved { get; set; } = false;

    /// <summary>Скрыта ли рецензия (удалена модератором)</summary>
    public bool IsHidden { get; set; } = false;

    /// <summary>Видна ли рецензия публично (или только модераторам)</summary>
    public bool IsPublic { get; set; } = false;

    /// <summary>Когда была создана рецензия</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Когда рецензия была одобрена</summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>Кто одобрил рецензию</summary>
    public string? ApprovedBy { get; set; }

    /// <summary>Количество полезных голосов</summary>
    public int HelpfulCount { get; set; } = 0;

    /// <summary>Может ли быть ответ от автора работы</summary>
    public string? AuthorResponse { get; set; }
}
