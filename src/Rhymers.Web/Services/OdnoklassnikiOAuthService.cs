using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rhymers.Core.Models;

namespace Rhymers.Web.Services;

/// <summary>
/// Сервис для интеграции с Одноклассниками OAuth
/// </summary>
public sealed class OdnoklassnikiOAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OdnoklassnikiOAuthService> _logger;
    private readonly HttpClient _httpClient;

    public OdnoklassnikiOAuthService(
        IConfiguration configuration,
        ILogger<OdnoklassnikiOAuthService> logger,
        HttpClient httpClient)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Получить URL для редиректа на Одноклассники
    /// </summary>
    public string GetAuthorizationUrl(string state, string redirectUri)
    {
        var clientId = _configuration["OAuth:Odnoklassniki:ClientId"];
        if (string.IsNullOrEmpty(clientId))
            throw new InvalidOperationException("OAuth:Odnoklassniki:ClientId не настроен");

        return $"https://www.odnoklassniki.ru/oauth/authorize" +
            $"?client_id={Uri.EscapeDataString(clientId)}" +
            $"&response_type=code" +
            $"&scope=VALUABLE_ACCESS,GET_EMAIL" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&state={Uri.EscapeDataString(state)}";
    }

    /// <summary>
    /// Обменять код на токен доступа
    /// </summary>
    public async Task<OdnoklassnikiTokenResponse?> ExchangeCodeForTokenAsync(
        string code,
        string redirectUri)
    {
        try
        {
            var clientId = _configuration["OAuth:Odnoklassniki:ClientId"];
            var clientSecret = _configuration["OAuth:Odnoklassniki:ClientSecret"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                throw new InvalidOperationException("Одноклассники OAuth конфигурация неполная");

            var requestBody = new Dictionary<string, string>
            {
                { "code", code },
                { "redirect_uri", redirectUri },
                { "grant_type", "authorization_code" },
                { "client_id", clientId },
                { "client_secret", clientSecret }
            };

            var content = new FormUrlEncodedContent(requestBody);
            var response = await _httpClient.PostAsync("https://api.odnoklassniki.ru/oauth/token.do", content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Ошибка при обмене кода на токен: {StatusCode}", response.StatusCode);
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<OdnoklassnikiTokenResponse>(stream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обмене кода на токен");
            return null;
        }
    }

    /// <summary>
    /// Получить информацию о текущем пользователе
    /// </summary>
    public async Task<OdnoklassnikiUserInfo?> GetCurrentUserAsync(string accessToken)
    {
        try
        {
            var clientId = _configuration["OAuth:Odnoklassniki:ClientId"];
            var clientSecret = _configuration["OAuth:Odnoklassniki:ClientSecret"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                throw new InvalidOperationException("Одноклассники OAuth конфигурация неполная");

            // Получить UID текущего пользователя
            var uidUrl = $"https://api.odnoklassniki.ru/fb.do" +
                $"?access_token={Uri.EscapeDataString(accessToken)}" +
                $"&method=users.getCurrentUser";

            var uidResponse = await _httpClient.GetAsync(uidUrl);
            if (!uidResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Ошибка при получении UID пользователя");
                return null;
            }

            using var uidStream = await uidResponse.Content.ReadAsStreamAsync();
            var uidJson = await JsonSerializer.DeserializeAsync<JsonElement>(uidStream);

            if (!uidJson.TryGetProperty("uid", out var uidProperty))
            {
                _logger.LogError("UID не найден в ответе");
                return null;
            }

            var uid = uidProperty.GetString();

            // Получить информацию о пользователе
            var signatureText = $"users.getInfo{uid}{clientSecret}";
            var signature = GetMD5Hash(signatureText);

            var userUrl = $"https://api.odnoklassniki.ru/fb.do" +
                $"?access_token={Uri.EscapeDataString(accessToken)}" +
                $"&method=users.getInfo" +
                $"&uids={Uri.EscapeDataString(uid)}" +
                $"&fields=uid,first_name,last_name,email,pic_uri" +
                $"&sig={Uri.EscapeDataString(signature)}";

            var userResponse = await _httpClient.GetAsync(userUrl);
            if (!userResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Ошибка при получении информации о пользователе");
                return null;
            }

            using var userStream = await userResponse.Content.ReadAsStreamAsync();
            var usersArray = await JsonSerializer.DeserializeAsync<JsonElement[]>(userStream);

            if (usersArray == null || usersArray.Length == 0)
            {
                _logger.LogError("Информация о пользователе не найдена");
                return null;
            }

            var userJson = usersArray[0];

            return new OdnoklassnikiUserInfo
            {
                Uid = userJson.TryGetProperty("uid", out var uidProp) ? uidProp.GetString() : null,
                FirstName = userJson.TryGetProperty("first_name", out var fnProp) ? fnProp.GetString() : null,
                LastName = userJson.TryGetProperty("last_name", out var lnProp) ? lnProp.GetString() : null,
                Email = userJson.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null,
                PhotoUri = userJson.TryGetProperty("pic_uri", out var photoProp) ? photoProp.GetString() : null,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении информации о пользователе");
            return null;
        }
    }

    private static string GetMD5Hash(string input)
    {
        using (var md5 = MD5.Create())
        {
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}

public class OdnoklassnikiTokenResponse
{
    public string? access_token { get; set; }
    public int expires_in { get; set; }
    public string? refresh_token { get; set; }
    public string? token_type { get; set; }
}

public class OdnoklassnikiUserInfo
{
    public string? Uid { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? PhotoUri { get; set; }

    public string GetDisplayName() => $"{FirstName} {LastName}".Trim();
}
