# OAuth Интеграция - Одноклассники

## 📋 Описание

Система регистрации и входа через Одноклассники (ОК). Позволяет пользователям авторизоваться одним кликом, используя аккаунт Одноклассников.

## 🔐 Требования

### Шаг 1: Регистрация приложения в Одноклассниках

1. Перейти на https://dev.odnoklassniki.ru/
2. Создать новое приложение
3. Получить **App ID** (CLIENT_ID) и **App Secret** (CLIENT_SECRET)
4. Добавить Redirect URI:
   ```
   http://localhost:5247/auth/oauth/odnoklassniki/callback
   (для production: https://your-domain.com/auth/oauth/odnoklassniki/callback)
   ```

### Шаг 2: Конфигурация приложения

Обновить `appsettings.json` или `appsettings.Development.json`:

```json
{
  "OAuth": {
    "Odnoklassniki": {
      "ClientId": "YOUR_ODNOKLASSNIKI_APP_ID",
      "ClientSecret": "YOUR_ODNOKLASSNIKI_APP_SECRET"
    }
  }
}
```

Или через переменные окружения:
```bash
export OAuth__Odnoklassniki__ClientId="YOUR_APP_ID"
export OAuth__Odnoklassniki__ClientSecret="YOUR_APP_SECRET"
```

## 🏗️ Архитектура

### OdnoklassnikiOAuthService
Сервис для работы с Одноклассники OAuth API:

```csharp
// Получить URL для редиректа на Одноклассники
string GetAuthorizationUrl(string state, string redirectUri)

// Обменять код на токен доступа
Task<OdnoklassnikiTokenResponse?> ExchangeCodeForTokenAsync(string code, string redirectUri)

// Получить информацию о пользователе
Task<OdnoklassnikiUserInfo?> GetCurrentUserAsync(string accessToken)
```

### OAuthController
Контроллер для обработки OAuth:

- `GET /auth/oauth/odnoklassniki/login` — Инициировать вход
- `GET /auth/oauth/odnoklassniki/callback` — Callback от Одноклассников

### Workflow

```
1. Пользователь нажимает "Войти через Одноклассники"
   ↓
2. Редирект на /auth/oauth/odnoklassniki/login
   ↓
3. Генерируется URL авторизации
   ↓
4. Редирект на Одноклассники (https://odnoklassniki.ru/oauth/authorize)
   ↓
5. Пользователь логинится в Одноклассниках
   ↓
6. Одноклассники редиректят на /auth/oauth/odnoklassniki/callback?code=...
   ↓
7. Обменять код на токен (POST к https://api.odnoklassniki.ru/oauth/token.do)
   ↓
8. Получить информацию о пользователе (API call)
   ↓
9. Найти/создать пользователя в системе
   ↓
10. Установить сессию
   ↓
11. Редирект на /profile
```

## 👤 Обработка пользователя

### При первом входе
- Пользователю автоматически присваивается роль **Reader**
- Username: `ok_{UID_ОДНОКЛАССНИКОВ}`
- DisplayName: Имя и фамилия из Одноклассников
- Email: Email из Одноклассников (если предоставлен)

### При повторных входах
- Проверяется наличие пользователя по `ok_{UID}`
- Обновляется информация профиля
- Если деактивирован, активируется заново

## 🔍 Запросы к API Одноклассников

### 1. Получение UID текущего пользователя
```
GET https://api.odnoklassniki.ru/fb.do?access_token={TOKEN}&method=users.getCurrentUser
```

### 2. Получение информации пользователя
```
GET https://api.odnoklassniki.ru/fb.do?
  access_token={TOKEN}
  &method=users.getInfo
  &uids={UID}
  &fields=uid,first_name,last_name,email,pic_uri
  &sig={MD5_SIGNATURE}
```

Подпись (sig) вычисляется как:
```
MD5(users.getInfo{UID}{ClientSecret})
```

## 🔗 Links

| Ресурс | URL |
|--------|-----|
| Портал разработчиков | https://dev.odnoklassniki.ru/ |
| OAuth документация | https://dev.odnoklassniki.ru/wiki/display/ok/OAuth+2.0 |
| API документация | https://dev.odnoklassniki.ru/wiki/display/ok/REST+API |
| Приложения | https://ok.ru/app/ |

## 🛠️ Troubleshooting

### Ошибка: "Одноклассники OAuth конфигурация неполная"
- Проверить что ClientId и ClientSecret установлены в appsettings
- Проверить переменные окружения если используются

### Ошибка: "oauth_error - invalid_request"
- Проверить что redirect_uri совпадает с зарегистрированным в приложении

### Ошибка: "UID не найден в ответе"
- Может быть проблема с правами доступа приложения
- Проверить что scope включает VALUABLE_ACCESS

### Пользователь создаётся как Reader
- Это намеренное поведение из соображений безопасности
- Администратор может повысить роль в админ-панели

## 📊 База данных

OAuth пользователи хранятся как обычные User с:
- Username: `ok_{UID}`
- PasswordHash: пусто (автоматически заполняется при необходимости)
- IsActive: true
- Role: Reader (изначально)

Система поддерживает как локальную авторизацию, так и OAuth параллельно.

## 🔄 Session Management

Используется ASP.NET Core SessionMiddleware:
- Timeout: 30 минут неактивности
- HttpOnly cookies
- Essential (обязательный для работы)

## 🎨 UI

### Login.razor
- Кнопка "Войти через Одноклассники" 
- Цвет: оранжевый (#ed812f) по стилю ОК
- Ниже стандартного поля входа по паролю

## 📝 Примеры

### Вход новым пользователем
1. Нажать "Войти через Одноклассники"
2. Авторизоваться в ОК
3. Система создаст пользователя ok_123456789
4. Редирект на /profile

### Повторный вход
1. Нажать "Войти через Одноклассники"
2. Если уже авторизован в ОК, мгновенный редирект на /profile
3. Если не авторизован, просит логин в ОК

## 🔒 Безопасность

- Используется State параметр для защиты от CSRF
- Все запросы к API Одноклассников используют HTTPS
- Токены не сохраняются в БД (используются только для получения информации)
- Пароли OAuth пользователей не требуются

## 📌 Production Deployment

При развёртывании в production:
1. Изменить Redirect URI на https://your-domain.com/auth/oauth/odnoklassniki/callback
2. Обновить https://dev.odnoklassniki.ru/ с новым URI
3. Установить ClientId и ClientSecret через переменные окружения
4. Убедиться что HTTPS включён
5. Протестировать полный workflow
