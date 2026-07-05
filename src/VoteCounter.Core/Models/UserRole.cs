namespace VoteCounter.Core.Models;

/// <summary>
/// Роли пользователей в системе
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Читатель/оценщик - доступ к конкурсной странице и истории
    /// </summary>
    Reader = 0,

    /// <summary>
    /// Автор - может подавать работы, доступ к своему профилю
    /// </summary>
    Author = 1,

    /// <summary>
    /// Модератор - назначается Admin'ом, проверяет работы на модерацию
    /// </summary>
    Moderator = 2,

    /// <summary>
    /// Администратор - все права, управляет другими модераторами
    /// </summary>
    Admin = 3
}
