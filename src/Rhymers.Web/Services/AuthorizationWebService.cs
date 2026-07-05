using Rhymers.Core.Models;
using Rhymers.Core.Services;

namespace Rhymers.Web.Services;

/// <summary>
/// Сервис для работы с авторизацией и управлением ролями в Blazor
/// </summary>
public sealed class AuthorizationWebService
{
    private readonly RoleAuthorizationService _authService;

    public AuthorizationWebService(RoleAuthorizationService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Зарегистрировать нового пользователя с паролем
    /// </summary>
    public async Task<User> RegisterUserAsync(string username, string displayName, string email, string password, UserRole role = UserRole.Reader)
    {
        var user = _authService.RegisterUser(username, displayName, email, password, role);
        return await Task.FromResult(user);
    }

    /// <summary>
    /// Логин пользователя по паролю
    /// </summary>
    public async Task<(bool success, string? error)> LoginAsync(string username, string password)
    {
        var (success, error) = _authService.LoginAsync(username, password);
        return await Task.FromResult((success, error));
    }

    /// <summary>
    /// Получить пользователя по имени
    /// </summary>
    public async Task<User?> GetUserAsync(string username)
    {
        var user = _authService.GetUser(username);
        return await Task.FromResult(user);
    }

    /// <summary>
    /// Установить текущего пользователя (симуляция логина)
    /// </summary>
    public async Task SetCurrentUserAsync(User? user)
    {
        _authService.SetCurrentUser(user);
        return;
    }

    /// <summary>
    /// Получить текущего пользователя
    /// </summary>
    public async Task<User?> GetCurrentUserAsync()
    {
        var user = _authService.GetCurrentUser();
        return await Task.FromResult(user);
    }

    /// <summary>
    /// Проверить, может ли пользователь выполнить действие
    /// </summary>
    public async Task<(bool IsAllowed, string? DenyReason)> CheckAuthorizationAsync(string action)
    {
        var result = _authService.CheckAuthorization(action);
        return await Task.FromResult((result.IsAllowed, result.DenyReason));
    }

    /// <summary>
    /// Проверить, может ли пользователь модерировать конкурс
    /// </summary>
    public async Task<bool> CanModerateContestAsync(User? user, string contestId)
    {
        var canModerate = _authService.CanModerateContest(user, contestId);
        return await Task.FromResult(canModerate);
    }

    /// <summary>
    /// Назначить модератора на конкурсы (Admin only)
    /// </summary>
    public async Task<bool> AssignModeratorAsync(User? adminUser, string username, List<string> contestIds)
    {
        if (adminUser == null || adminUser.Role != UserRole.Admin)
            return false;

        var result = _authService.AssignModerator(adminUser, username, contestIds);
        return await Task.FromResult(result);
    }

    /// <summary>
    /// Удалить роль модератора (Admin only)
    /// </summary>
    public async Task<bool> RemoveModeratorAsync(User? adminUser, string username)
    {
        if (adminUser == null || adminUser.Role != UserRole.Admin)
            return false;

        var result = _authService.RemoveModerator(adminUser, username);
        return await Task.FromResult(result);
    }

    /// <summary>
    /// Отключить учётную запись (Admin only)
    /// </summary>
    public async Task<bool> DisableUserAsync(User? adminUser, string username, string? reason = null)
    {
        if (adminUser == null || adminUser.Role != UserRole.Admin)
            return false;

        var result = _authService.DisableUser(adminUser, username, reason);
        return await Task.FromResult(result);
    }

    /// <summary>
    /// Получить всех пользователей (Admin only)
    /// </summary>
    public async Task<List<User>> GetAllUsersAsync(User? adminUser)
    {
        var users = _authService.GetAllUsers(adminUser);
        return await Task.FromResult(users);
    }

    /// <summary>
    /// Получить всех активных модераторов (Admin only)
    /// </summary>
    public async Task<List<User>> GetAllModeratorsAsync(User? adminUser)
    {
        var moderators = _authService.GetAllModerators(adminUser);
        return await Task.FromResult(moderators);
    }
}
