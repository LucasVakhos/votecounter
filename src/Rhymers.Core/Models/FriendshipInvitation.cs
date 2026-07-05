namespace Rhymers.Core.Models;

/// <summary>
/// Приглашение в друзья от одного пользователя к другому
/// </summary>
public sealed class FriendshipInvitation
{
    /// <summary>Уникальный идентификатор приглашения</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>ID пользователя, который отправил приглашение</summary>
    public string FromUserId { get; set; } = string.Empty;
    
    /// <summary>ID пользователя, который получил приглашение</summary>
    public string ToUserId { get; set; } = string.Empty;
    
    /// <summary>Статус приглашения</summary>
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    
    /// <summary>Опциональное сообщение в приглашении</summary>
    public string? Message { get; set; }
    
    /// <summary>Когда было отправлено приглашение</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Когда приглашение было обработано (принято/отклонено)</summary>
    public DateTime? ProcessedAt { get; set; }
    
    /// <summary>Кто обработал приглашение (автоматически заполняется при принятии/отклонении)</summary>
    public string? ProcessedBy { get; set; }
}

/// <summary>
/// Статус приглашения в друзья
/// </summary>
public enum InvitationStatus
{
    /// <summary>Ожидает ответа</summary>
    Pending = 0,
    
    /// <summary>Принято</summary>
    Accepted = 1,
    
    /// <summary>Отклонено</summary>
    Declined = 2,
    
    /// <summary>Отменено отправителем</summary>
    Cancelled = 3
}
