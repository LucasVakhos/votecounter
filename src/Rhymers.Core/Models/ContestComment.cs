namespace Rhymers.Core.Models;

/// <summary>
/// Комментарий/обсуждение около-конкурсных переживаний.
/// </summary>
/// <remarks>
/// Позволяет участникам, зрителям и модераторам обсуждать конкурс в целом,
/// делиться впечатлениями, вопросами и идеями.
/// </remarks>
public sealed class ContestComment
{
    /// <summary>Уникальный идентификатор комментария</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>ID конкурса, к которому относится комментарий</summary>
    public string ContestId { get; set; } = string.Empty;

    /// <summary>Имя автора комментария</summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>Роль автора (для фильтрации по авторитету)</summary>
    public UserRole AuthorRole { get; set; } = UserRole.Reader;

    /// <summary>Текст комментария</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Одобрен ли комментарий модератором</summary>
    public bool IsApproved { get; set; } = false;

    /// <summary>Скрыт ли комментарий (удален модератором)</summary>
    public bool IsHidden { get; set; } = false;

    /// <summary>Когда был создан комментарий</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Когда был последний раз отредактирован</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Когда был одобрен модератором</summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>Имя модератора, одобрившего комментарий</summary>
    public string? ApprovedBy { get; set; }

    /// <summary>Количество лайков/одобрений от других пользователей</summary>
    public int LikeCount { get; set; } = 0;

    /// <summary>ID родительского комментария (для ответов в цепочку)</summary>
    public string? ParentCommentId { get; set; }
}
