namespace Rhymers.Core.Models;

/// <summary>
/// Пользователь системы с информацией о роли и доступе
/// </summary>
public sealed class User
{
    /// <summary>
    /// Уникальный идентификатор пользователя
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Имя пользователя (логин)
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Хеш пароля (BCrypt)
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Дисплей-имя (для отображения в интерфейсе)
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Email пользователя
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Роль пользователя в системе
    /// </summary>
    public UserRole Role { get; set; } = UserRole.Reader;

    /// <summary>
    /// Активна ли учётная запись
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Когда была создана учётная запись
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Когда был последний вход
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Дополнительные примечания (для Admin)
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Для авторов - ID их профиля
    /// </summary>
    public string? AuthorProfileId { get; set; }

    /// <summary>
    /// Список конкурсов, в которых модератор может проверять работы
    /// </summary>
    public List<string> ModeratedContests { get; set; } = new();

    public User Clone()
    {
        return new User
        {
            Id = Id,
            Username = Username,
            DisplayName = DisplayName,
            Email = Email,
            Role = Role,
            IsActive = IsActive,
            CreatedAt = CreatedAt,
            LastLoginAt = LastLoginAt,
            Notes = Notes,
            AuthorProfileId = AuthorProfileId,
            ModeratedContests = new List<string>(ModeratedContests)
            // PasswordHash НЕ копируем для безопасности
        };
    }

    public override string ToString() => $"{DisplayName} ({Username}) - {Role}";
}
