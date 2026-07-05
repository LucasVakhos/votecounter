namespace Rhymers.Core.Models;

/// <summary>
/// Приватное сообщение между двумя пользователями (друзьями)
/// </summary>
public sealed class UserDirectMessage
{
    /// <summary>Уникальный идентификатор сообщения</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>ID пользователя-отправителя</summary>
    public string FromUserId { get; set; } = string.Empty;
    
    /// <summary>ID пользователя-получателя</summary>
    public string ToUserId { get; set; } = string.Empty;
    
    /// <summary>Содержание сообщения (текст + возможные ссылки на вложения)</summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>Прочитано ли сообщение получателем</summary>
    public bool IsRead { get; set; } = false;
    
    /// <summary>Когда было прочитано</summary>
    public DateTime? ReadAt { get; set; }
    
    /// <summary>Когда было отправлено сообщение</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Когда было отредактировано (если редактировалось)</summary>
    public DateTime? EditedAt { get; set; }
    
    /// <summary>Удалено ли сообщение отправителем</summary>
    public bool IsDeletedBySender { get; set; } = false;
    
    /// <summary>Удалено ли сообщение получателем</summary>
    public bool IsDeletedByRecipient { get; set; } = false;
    
    /// <summary>ID родительского сообщения (если это ответ/цитата)</summary>
    public string? ParentMessageId { get; set; }
}
