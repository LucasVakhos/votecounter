namespace Rhymers.Core.Models;

/// <summary>
/// Дружба между двумя пользователями
/// </summary>
public sealed class UserFriendship
{
    /// <summary>Уникальный идентификатор записи о дружбе</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>ID первого пользователя (инициатор дружбы)</summary>
    public string UserId1 { get; set; } = string.Empty;
    
    /// <summary>ID второго пользователя (получатель)</summary>
    public string UserId2 { get; set; } = string.Empty;
    
    /// <summary>Статус дружбы (активна или нет)</summary>
    public FriendshipStatus Status { get; set; } = FriendshipStatus.Active;
    
    /// <summary>Когда была установлена дружба</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Когда дружба была обновлена</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Примечания или причина блокировки</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Получить "другого" пользователя в паре дружбы
    /// </summary>
    public string GetOtherId(string currentUserId)
    {
        return currentUserId == UserId1 ? UserId2 : UserId1;
    }
}

/// <summary>
/// Статус дружбы между пользователями
/// </summary>
public enum FriendshipStatus
{
    /// <summary>Активная дружба</summary>
    Active = 0,
    
    /// <summary>Заблокирована первым пользователем</summary>
    BlockedByUser1 = 1,
    
    /// <summary>Заблокирована вторым пользователем</summary>
    BlockedByUser2 = 2,
    
    /// <summary>Дружба удалена</summary>
    Deleted = 3
}
