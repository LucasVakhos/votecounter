namespace Rhymers.Core.Services;

using Rhymers.Core.Models;
using Rhymers.Core.Data;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Результат проверки доступа
/// </summary>
public sealed class AuthorizationResult
{
    public bool IsAllowed { get; set; }
    public string? DenyReason { get; set; }
    
    public static AuthorizationResult Allow() => new() { IsAllowed = true };
    public static AuthorizationResult Deny(string reason) => new() { IsAllowed = false, DenyReason = reason };
}

/// <summary>
/// Сервис проверки прав доступа и управления ролями
/// </summary>
public sealed class RoleAuthorizationService
{
    private readonly RhymersDbContext _context;
    private readonly PasswordHasher _passwordHasher;
    private User? _currentUser;

    public RoleAuthorizationService(RhymersDbContext context, PasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    /// <summary>
    /// Зарегистрировать пользователя с паролем
    /// </summary>
    public User RegisterUser(string username, string displayName, string email, string password, UserRole role = UserRole.Reader)
    {
        var existingUser = _context.Users.FirstOrDefault(u => u.Username == username);
        if (existingUser != null)
            throw new InvalidOperationException($"Пользователь '{username}' уже существует");

        var user = new User
        {
            Username = username,
            DisplayName = displayName,
            Email = email,
            PasswordHash = _passwordHasher.HashPassword(password),
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    /// <summary>
    /// Логин пользователя по паролю
    /// </summary>
    public (bool success, string? error) LoginAsync(string username, string password)
    {
        var user = _context.Users.FirstOrDefault(u => u.Username == username);
        
        if (user == null)
            return (false, "Пользователь не найден");

        if (!user.IsActive)
            return (false, "Учётная запись отключена");

        if (!_passwordHasher.VerifyPassword(password, user.PasswordHash))
            return (false, "Неверный пароль");

        // Логин успешен
        SetCurrentUser(user);
        return (true, null);
    }

    /// <summary>
    /// Получить пользователя по имени
    /// </summary>
    public User? GetUser(string username)
    {
        return _context.Users.FirstOrDefault(u => u.Username == username && u.IsActive);
    }

    /// <summary>
    /// Установить текущего пользователя (симуляция логина)
    /// </summary>
    public void SetCurrentUser(User? user)
    {
        _currentUser = user;
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            _context.Users.Update(user);
            _context.SaveChanges();
        }
    }

    /// <summary>
    /// Получить текущего пользователя
    /// </summary>
    public User? GetCurrentUser() => _currentUser;

    /// <summary>
    /// Проверить, имеет ли пользователь роль
    /// </summary>
    public bool HasRole(User? user, UserRole requiredRole)
    {
        if (user == null) return false;
        if (!user.IsActive) return false;
        
        // Admin имеет все права
        if (user.Role == UserRole.Admin) return true;
        
        // Проверяем точное совпадение или выше
        return user.Role >= requiredRole;
    }

    /// <summary>
    /// Проверить, может ли пользователь модерировать конкурс
    /// </summary>
    public bool CanModerateContest(User? user, string contestId)
    {
        if (user == null) return false;
        if (!user.IsActive) return false;
        
        // Admin может модерировать любой конкурс
        if (user.Role == UserRole.Admin) return true;
        
        // Moderator может модерировать только назначенные конкурсы
        if (user.Role == UserRole.Moderator)
            return user.ModeratedContests.Contains(contestId);
        
        return false;
    }

    /// <summary>
    /// Получить авторизацию на действие для текущего пользователя
    /// </summary>
    public AuthorizationResult CheckAuthorization(string action)
    {
        if (_currentUser == null)
            return AuthorizationResult.Deny("Пользователь не авторизован");

        if (!_currentUser.IsActive)
            return AuthorizationResult.Deny("Учётная запись отключена");

        return action switch
        {
            "view_contests" => _currentUser.Role >= UserRole.Reader 
                ? AuthorizationResult.Allow()
                : AuthorizationResult.Deny("Недостаточно прав"),

            "submit_work" => _currentUser.Role >= UserRole.Author
                ? AuthorizationResult.Allow()
                : AuthorizationResult.Deny("Только авторы могут подавать работы"),

            "view_own_profile" => _currentUser.Role >= UserRole.Author
                ? AuthorizationResult.Allow()
                : AuthorizationResult.Deny("Авторы могут просматривать свой профиль"),

            "moderate_work" => _currentUser.Role >= UserRole.Moderator
                ? AuthorizationResult.Allow()
                : AuthorizationResult.Deny("Только модераторы могут проверять работы"),

            "manage_moderators" => _currentUser.Role == UserRole.Admin
                ? AuthorizationResult.Allow()
                : AuthorizationResult.Deny("Только администраторы могут управлять модераторами"),

            "admin_panel" => _currentUser.Role == UserRole.Admin
                ? AuthorizationResult.Allow()
                : AuthorizationResult.Deny("Доступ только для администраторов"),

            _ => AuthorizationResult.Deny($"Неизвестное действие: {action}")
        };
    }

    /// <summary>
    /// Назначить модератора (Admin only)
    /// </summary>
    public bool AssignModerator(User? adminUser, string username, List<string> contestIds)
    {
        if (adminUser?.Role != UserRole.Admin)
            return false;

        var user = _context.Users.FirstOrDefault(u => u.Username == username);
        if (user == null)
            return false;

        user.Role = UserRole.Moderator;
        user.ModeratedContests = contestIds;
        _context.Users.Update(user);
        _context.SaveChanges();
        return true;
    }

    /// <summary>
    /// Удалить роль модератора (Admin only)
    /// </summary>
    public bool RemoveModerator(User? adminUser, string username)
    {
        if (adminUser?.Role != UserRole.Admin)
            return false;

        var user = _context.Users.FirstOrDefault(u => u.Username == username);
        if (user == null)
            return false;

        user.Role = UserRole.Author;
        user.ModeratedContests.Clear();
        _context.Users.Update(user);
        _context.SaveChanges();
        return true;
    }

    /// <summary>
    /// Отключить учётную запись (Admin only)
    /// </summary>
    public bool DisableUser(User? adminUser, string username, string? reason = null)
    {
        if (adminUser?.Role != UserRole.Admin)
            return false;

        var user = _context.Users.FirstOrDefault(u => u.Username == username);
        if (user == null)
            return false;

        user.IsActive = false;
        user.Notes = reason;
        _context.Users.Update(user);
        _context.SaveChanges();
        return true;
    }

    /// <summary>
    /// Получить всех пользователей (Admin only)
    /// </summary>
    public List<User> GetAllUsers(User? adminUser)
    {
        if (adminUser?.Role != UserRole.Admin)
            return new();

        return _context.Users.ToList();
    }

    /// <summary>
    /// Получить всех модераторов (Admin only)
    /// </summary>
    public List<User> GetAllModerators(User? adminUser)
    {
        if (adminUser?.Role != UserRole.Admin)
            return new();

        return _context.Users.Where(u => u.Role == UserRole.Moderator && u.IsActive).ToList();
    }

    /// <summary>
    /// Получить всех пользователей (Developer only)
    /// </summary>
    public List<User> GetAllUsersForDeveloper()
    {
        return _context.Users.OrderByDescending(u => u.Role).ThenBy(u => u.DisplayName).ToList();
    }

    /// <summary>
    /// Изменить роль пользователя (Developer only)
    /// </summary>
    public bool UpdateUserRole(string userId, UserRole newRole)
    {
        var user = _context.Users.FirstOrDefault(u => u.Id == userId);
        if (user == null)
            return false;

        user.Role = newRole;
        _context.Users.Update(user);
        _context.SaveChanges();
        return true;
    }
}
