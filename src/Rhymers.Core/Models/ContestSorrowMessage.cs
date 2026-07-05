namespace Rhymers.Core.Models;

/// <summary>
/// Сообщение в чате "Страсти по рифме" - обсуждение переживаний и впечатлений о конкурсе
/// </summary>
/// <remarks>
/// Предназначен для свободного обсуждения участниками своих эмоций, страхов, радостей 
/// и впечатлений от участия в конкурсе. Отдельный от контеста, посвященный личным переживаниям.
/// </remarks>
public sealed class ContestSorrowMessage
{
    /// <summary>Уникальный идентификатор сообщения</summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>ID конкурса, к которому относится переживание</summary>
    public string ContestId { get; set; } = string.Empty;
    
    /// <summary>Имя автора сообщения (может быть анонимным)</summary>
    public string AuthorName { get; set; } = string.Empty;
    
    /// <summary>Роль автора в системе</summary>
    public UserRole AuthorRole { get; set; }
    
    /// <summary>Содержание переживания/впечатления</summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>Одобрено ли модератором (может быть скрыто как спам/оскорбления)</summary>
    public bool IsApproved { get; set; } = true;
    
    /// <summary>Скрыто ли сообщение из публичного доступа</summary>
    public bool IsHidden { get; set; } = false;
    
    /// <summary>Когда было отправлено сообщение</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>Когда было обновлено</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    
    /// <summary>Когда было одобрено модератором</summary>
    public DateTime? ApprovedAt { get; set; }
    
    /// <summary>Кто одобрил (модератор)</summary>
    public string? ApprovedBy { get; set; }
    
    /// <summary>Количество "поддержек" (like-и) от других участников</summary>
    public int EmpathyCount { get; set; } = 0;
    
    /// <summary>ID родительского сообщения (для поддержек/ответов)</summary>
    public string? ParentMessageId { get; set; }
    
    /// <summary>Тип переживания (для категоризации)</summary>
    public SorrowType Type { get; set; } = SorrowType.Reflection;

    public ContestSorrowMessage Clone()
    {
        return new ContestSorrowMessage
        {
            Id = Id,
            ContestId = ContestId,
            AuthorName = AuthorName,
            AuthorRole = AuthorRole,
            Content = Content,
            IsApproved = IsApproved,
            IsHidden = IsHidden,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            ApprovedAt = ApprovedAt,
            ApprovedBy = ApprovedBy,
            EmpathyCount = EmpathyCount,
            ParentMessageId = ParentMessageId,
            Type = Type
        };
    }
}

/// <summary>
/// Тип переживания/сообщения в чате "Страсти по рифме"
/// </summary>
public enum SorrowType
{
    /// <summary>Размышление, рефлексия</summary>
    Reflection = 0,
    
    /// <summary>Страх, волнение</summary>
    Fear = 1,
    
    /// <summary>Разочарование, грусть</summary>
    Disappointment = 2,
    
    /// <summary>Вдохновение, энтузиазм</summary>
    Inspiration = 3,
    
    /// <summary>Поддержка, ободрение других</summary>
    Support = 4,
    
    /// <summary>Анализ своей работы</summary>
    SelfAnalysis = 5,
    
    /// <summary>Впечатления от других работ</summary>
    Impressions = 6,
    
    /// <summary>Жизненные обстоятельства, влияющие на творчество</summary>
    LifeCircumstances = 7
}
