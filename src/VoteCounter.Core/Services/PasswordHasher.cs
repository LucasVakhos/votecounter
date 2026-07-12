using BCrypt.Net;

namespace VoteCounter.Core.Services;

/// <summary>
/// Сервис для хеширования и проверки паролей
/// </summary>
public sealed class PasswordHasher
{
    /// <summary>
    /// Захешировать пароль
    /// </summary>
    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Пароль не может быть пустым", nameof(password));

        // BCrypt автоматически генерирует salt и применяет хеширование
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    /// <summary>
    /// Проверить пароль против хеша
    /// </summary>
    public bool VerifyPassword(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;

        if (string.IsNullOrWhiteSpace(hash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            // Если хеш некорректный, вернуть false
            return false;
        }
    }
}
