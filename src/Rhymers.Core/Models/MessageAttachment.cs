namespace Rhymers.Core.Models;

/// <summary>
/// Вложение (картинка, смайлик) в сообщение (чат или приватное сообщение)
/// </summary>
public sealed class MessageAttachment
{
    /// <summary>Уникальный идентификатор вложения</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>ID сообщения, к которому относится вложение</summary>
    public string MessageId { get; set; } = string.Empty;
    
    /// <summary>Тип сообщения (чат конкурса или приватное сообщение)</summary>
    public MessageAttachmentType MessageType { get; set; } = MessageAttachmentType.SorrowChat;
    
    /// <summary>Тип вложения (картинка, смайлик и т.д.)</summary>
    public AttachmentType Type { get; set; } = AttachmentType.Image;
    
    /// <summary>Путь или URL файла (может быть локальный путь или URL)</summary>
    public string FileUrl { get; set; } = string.Empty;
    
    /// <summary>Оригинальное имя файла</summary>
    public string? FileName { get; set; }
    
    /// <summary>Размер файла в байтах</summary>
    public long FileSize { get; set; }
    
    /// <summary>MIME-тип файла</summary>
    public string? MimeType { get; set; }
    
    /// <summary>Когда было загруженно вложение</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Текст-описание или код смайлика (для смайликов)</summary>
    public string? AltText { get; set; }
}

/// <summary>
/// Тип сообщения, к которому относится вложение
/// </summary>
public enum MessageAttachmentType
{
    /// <summary>Сообщение в чате конкурса "Страсти по рифме"</summary>
    SorrowChat = 0,
    
    /// <summary>Приватное сообщение между друзьями</summary>
    DirectMessage = 1
}

/// <summary>
/// Тип вложения в сообщение
/// </summary>
public enum AttachmentType
{
    /// <summary>Изображение (JPG, PNG, GIF и т.д.)</summary>
    Image = 0,
    
    /// <summary>Смайлик/эмодзи</summary>
    Emoji = 1,
    
    /// <summary>Документ</summary>
    Document = 2,
    
    /// <summary>Видео</summary>
    Video = 3,
    
    /// <summary>Аудио</summary>
    Audio = 4
}
