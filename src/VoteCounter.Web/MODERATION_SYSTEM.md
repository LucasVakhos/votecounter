# Система модерации работ

## Обзор

Новая система позволяет поэтам самостоятельно выставлять свои работы, которые затем отправляются на модерацию перед добавлением в конкурс.

### Процесс подачи работы:
1. **Поэт** → Подает работу через форму "Подать работу"
2. **Система** → Сохраняет работу со статусом "На модерации"
3. **Модератор** → Проверяет в "Очереди на модерацию"
4. **Модератор** → Одобряет или отклоняет
5. **Система** → Уведомляет автора о решении

## Компоненты

### Модели (Core)

#### `WorkStatus.cs`
Перечисление статусов работ:
- `PendingModeration` (0) - Ожидает модерации
- `Approved` (1) - Одобрена, добавлена в конкурс  
- `Rejected` (2) - Отклонена модератором
- `UnderReview` (3) - На рассмотрении

#### `WorkSubmission.cs`
Класс для отслеживания подачи работы:
```csharp
public sealed class WorkSubmission
{
    public string Id { get; set; }                    // ID подачи
    public string ContestId { get; set; }            // ID конкурса
    public ContestWork Work { get; set; }            // Сама работа
    public WorkStatus Status { get; set; }           // Статус
    public DateTime SubmittedAt { get; set; }        // Время подачи
    public DateTime? ModeratedAt { get; set; }       // Время модерации
    public string? ModeratorName { get; set; }       // Имя модератора
    public string? ModerationNote { get; set; }      // Примечание/причина отклонения
}
```

#### Обновления `ContestWork.cs`
- Добавлено поле `Status: WorkStatus`
- Добавлено поле `SubmittedAt: DateTime`

### Сервисы (Core)

#### `ModerationService.cs`
Основной сервис для управления модерацией:

```csharp
// Подать новую работу
public WorkSubmission SubmitWork(string contestId, ContestWork work)

// Получить очередь работ на модерацию
public List<WorkSubmission> GetPendingSubmissions(string contestId)

// Получить все подачи конкурса
public List<WorkSubmission> GetAllSubmissions(string contestId)

// Одобрить работу
public bool ApproveSubmission(string contestId, string submissionId, string moderatorName)

// Отклонить работу
public bool RejectSubmission(string contestId, string submissionId, string reason, string moderatorName)

// Получить одобренные работы для конкурса
public List<ContestWork> GetApprovedWorks(string contestId)
```

### Web сервисы

#### `ModerationWebService.cs`
Обертка для использования в Blazor компонентах (асинхронная версия `ModerationService`).

## Страницы Blazor

### 📝 `/submit-work` - Подать работу
Для поэтов. Форма с полями:
- **Конкурс** (выпадающий список)
- **Имя автора** 
- **Название работы**
- **Тема** (выпадающий список)
- **Текст работы** (большой текстовый area)

При успешной подаче показывает ID подачи и перенаправляет на страницу статуса.

### 📋 `/work-status` - Мои работы
Для поэтов. Показывает статус всех подданных работ:
- **⏳ На модерации** - Работа ждет решения
- **✅ Одобрена** - Добавлена в конкурс
- **❌ Отклонена** - С указанием причины + кнопка "Подать исправленную версию"

### 🔍 `/moderation-queue` - Очередь на модерацию
Для модераторов. Показывает работы в статусе "На модерации":
- Карточки с информацией о работе
- Текст работы (с прокруткой)
- Поле для ввода причины отклонения
- Кнопки: **✓ Одобрить** и **✗ Отклонить**

При одобрении:
- Работа переходит в статус `Approved`
- Добавляется информация о модераторе и времени

При отклонении:
- Работа переходит в статус `Rejected`
- Сохраняется причина отклонения

## Интеграция в DI

**Program.cs:**
```csharp
// Регистрация ModerationService в Core
builder.Services.AddVoteCounterCore();  // Включает ModerationService

// Регистрация Web сервиса
builder.Services.AddScoped<ModerationWebService>();
```

**CoreServiceCollectionExtensions.cs:**
```csharp
services.AddSingleton<ModerationService>();
```

## Навигация

Добавлены три новые пункта в меню:
- 📝 Подать работу → `/submit-work`
- 📋 Мои работы → `/work-status`
- 🔍 Модерация → `/moderation-queue`

## Примеры использования

### Поэт подает работу
```csharp
var work = new ContestWork
{
    Title = "Весна",
    Author = "Иван Петров",
    Topic = "Nature",
    Content = "Текст стихотворения...",
    Status = WorkStatus.PendingModeration,
    SubmittedAt = DateTime.Now
};

var submission = await moderationService.SubmitWorkAsync("contest-1", work);
// submission.Id содержит уникальный ID подачи
```

### Модератор одобряет работу
```csharp
var result = await moderationService.ApproveWorkAsync(
    "contest-1", 
    submissionId: "abc123...", 
    moderatorName: "Admin"
);
```

### Модератор отклоняет работу
```csharp
var result = await moderationService.RejectWorkAsync(
    "contest-1",
    submissionId: "abc123...",
    reason: "Проверьте орфографию",
    moderatorName: "Admin"
);
```

### Получить одобренные работы
```csharp
var approvedWorks = await moderationService.GetApprovedWorksAsync("contest-1");
// Теперь эти работы могут быть добавлены в конкурс
```

## Хранение данных

Текущая реализация использует **in-memory хранилище** (словари):
```csharp
private readonly Dictionary<string, List<WorkSubmission>> _submissions = new();
```

Для production необходимо:
1. Перенести в `LocalDatabase` или `LocalStore`
2. Сохранять в SQLite базу
3. Добавить индексы для быстрого поиска по `ContestId` и `Status`

## Будущие улучшения

- [ ] Email-уведомления авторам о решении модератора
- [ ] История изменений статуса подачи
- [ ] Возможность редактирования отклоненной работы
- [ ] Статистика модерации (скорость обработки, процент отклонений)
- [ ] Автоматические проверки (проверка орфографии, длина текста и т.д.)
- [ ] Сохранение в базу данных вместо in-memory
