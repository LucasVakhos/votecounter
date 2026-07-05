using Microsoft.AspNetCore.Mvc;
using Rhymers.Web.Services;
using Rhymers.Core.Services;
using Rhymers.Core.Models;

namespace Rhymers.Web.Controllers;

[ApiController]
[Route("auth/oauth")]
public sealed class OAuthController : ControllerBase
{
    private readonly OdnoklassnikiOAuthService _oauthService;
    private readonly AuthorizationWebService _authService;
    private readonly PersistenceService _persistenceService;
    private readonly ILogger<OAuthController> _logger;

    public OAuthController(
        OdnoklassnikiOAuthService oauthService,
        AuthorizationWebService authService,
        PersistenceService persistenceService,
        ILogger<OAuthController> logger)
    {
        _oauthService = oauthService;
        _authService = authService;
        _persistenceService = persistenceService;
        _logger = logger;
    }

    /// <summary>
    /// Callback от Одноклассников после авторизации
    /// </summary>
    [HttpGet("odnoklassniki/callback")]
    public async Task<IActionResult> OdnoklassnikiCallback(
        [FromQuery] string? code,
        [FromQuery] string? error,
        [FromQuery] string? error_description,
        [FromQuery] string? state)
    {
        try
        {
            // Проверить ошибку
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("Ошибка от Одноклассников: {Error} - {Description}",
                    error, error_description);

                return Redirect($"/auth/login?error=oauth_error&message={Uri.EscapeDataString(error_description ?? error)}");
            }

            // Проверить код
            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("Код авторизации не получен");
                return Redirect("/auth/login?error=missing_code");
            }

            // Обменять код на токен
            var redirectUri = $"{Request.Scheme}://{Request.Host}/auth/oauth/odnoklassniki/callback";
            var tokenResponse = await _oauthService.ExchangeCodeForTokenAsync(code, redirectUri);

            if (tokenResponse?.access_token == null)
            {
                _logger.LogWarning("Не удалось получить токен доступа");
                return Redirect("/auth/login?error=token_failed");
            }

            // Получить информацию о пользователе
            var userInfo = await _oauthService.GetCurrentUserAsync(tokenResponse.access_token);
            if (userInfo == null || string.IsNullOrEmpty(userInfo.Uid))
            {
                _logger.LogWarning("Не удалось получить информацию о пользователе");
                return Redirect("/auth/login?error=user_info_failed");
            }

            // Проверить/создать пользователя
            var oauthUserEmail = userInfo.Email ?? $"ok_{userInfo.Uid}@odnoklassniki.local";
            var displayName = userInfo.GetDisplayName();

            var existingUser = await _persistenceService.GetUserByUsernameAsync($"ok_{userInfo.Uid}");

            User user;
            if (existingUser != null)
            {
                // Обновить существующего пользователя
                existingUser.Email = userInfo.Email ?? existingUser.Email;
                if (!string.IsNullOrEmpty(displayName) && displayName != existingUser.DisplayName)
                    existingUser.DisplayName = displayName;
                existingUser.IsActive = true;

                user = await _persistenceService.SaveUserAsync(existingUser);
                _logger.LogInformation("Пользователь {Username} вошёл через Одноклассники", existingUser.Username);
            }
            else
            {
                // Создать нового пользователя
                user = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = $"ok_{userInfo.Uid}",
                    DisplayName = !string.IsNullOrEmpty(displayName) ? displayName : $"ОК_{userInfo.Uid}",
                    Email = userInfo.Email,
                    PasswordHash = "", // OAuth пользователь не имеет пароля
                    Role = UserRole.Reader, // Новые пользователи - читатели
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                user = await _persistenceService.SaveUserAsync(user);
                _logger.LogInformation("Создан новый пользователь {Username} через Одноклассники", user.Username);
            }

            // Установить текущего пользователя
            await _authService.SetCurrentUserAsync(user);

            // Редиректить на профиль или главную
            return Redirect("/profile");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке OAuth callback");
            return Redirect($"/auth/login?error=callback_error");
        }
    }

    /// <summary>
    /// Инициировать вход через Одноклассники
    /// </summary>
    [HttpGet("odnoklassniki/login")]
    public IActionResult OdnoklassnikiLogin()
    {
        try
        {
            var state = Guid.NewGuid().ToString("N");
            var redirectUri = $"{Request.Scheme}://{Request.Host}/auth/oauth/odnoklassniki/callback";

            // Сохранить state в сессию для проверки
            HttpContext.Session.SetString("oauth_state", state);

            var authUrl = _oauthService.GetAuthorizationUrl(state, redirectUri);
            return Redirect(authUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при инициировании OAuth");
            return Redirect("/auth/login?error=init_failed");
        }
    }
}
