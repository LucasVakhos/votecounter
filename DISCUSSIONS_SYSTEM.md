# Система Дискуссий и Рецензий (Phase 8)

## 📋 Описание

Система параллельного чата около-конкурсных переживаний и публичных рецензий на работы. Позволяет участникам и зрителям:
- Обсуждать конкурс в целом (около-конкурсные переживания)
- Писать развёрнутые рецензии на конкретные работы
- Модерировать комментарии и рецензии

## 🏗️ Архитектура

### Модели данных

#### ContestComment
Комментарий к конкурсу для обсуждения:
```csharp
public string Id { get; set; }              // GUID
public string ContestId { get; set; }       // FK на Contest
public string AuthorName { get; set; }      // Отображаемое имя
public UserRole AuthorRole { get; set; }    // Роль автора для авторитета
public string Content { get; set; }         // Текст комментария
public bool IsApproved { get; set; }        // Одобрен модератором?
public bool IsHidden { get; set; }          // Скрыт от публики?
public DateTime CreatedAt { get; set; }
public DateTime? UpdatedAt { get; set; }
public DateTime? ApprovedAt { get; set; }
public string? ApprovedBy { get; set; }     // Кто одобрил
public int LikeCount { get; set; }          // Лайки пользователей
public string? ParentCommentId { get; set; }// Ответ в цепочку
```

#### WorkReview
Рецензия на конкретную работу:
```csharp
public string Id { get; set; }              // GUID
public string ContestId { get; set; }       // FK на Contest
public int WorkNumber { get; set; }         // Номер работы в конкурсе
public string WorkTitle { get; set; }       // Название работы
public string ReviewerName { get; set; }    // Кто писал рецензию
public UserRole ReviewerRole { get; set; }  // Роль рецензента
public string Title { get; set; }           // Заголовок рецензии
public string Content { get; set; }         // Основной текст
public int? Rating { get; set; }            // 1-5 звёзд
public string? Strengths { get; set; }      // Достоинства
public string? AreasForImprovement { get; set; }  // Для улучшения
public bool IsApproved { get; set; }        // Одобрена?
public bool IsHidden { get; set; }          // Скрыта?
public bool IsPublic { get; set; }          // Видна публике?
public DateTime CreatedAt { get; set; }
public DateTime? ApprovedAt { get; set; }
public string? ApprovedBy { get; set; }
public int HelpfulCount { get; set; }       // Полезные голосы
public string? AuthorResponse { get; set; } // Ответ автора на рецензию
```

### DiscussionService
Сервис для управления дискуссиями:

```csharp
// Комментарии к конкурсу
Task<ContestComment> AddContestCommentAsync(contestId, authorName, authorRole, content, parentCommentId?)
Task<List<ContestComment>> GetContestCommentsAsync(contestId, includeUnapproved?, includeHidden?)
Task<List<ContestComment>> GetCommentRepliesAsync(parentCommentId)
Task<bool> ApproveContestCommentAsync(commentId, moderatorName)
Task<bool> HideContestCommentAsync(commentId)
Task<bool> LikeContestCommentAsync(commentId)

// Рецензии на работы
Task<WorkReview> AddWorkReviewAsync(contestId, workNumber, workTitle, reviewerName, reviewerRole, title, content, rating?, strengths?, areas?)
Task<List<WorkReview>> GetWorkReviewsAsync(contestId, workNumber, includeUnapproved?)
Task<List<WorkReview>> GetContestReviewsAsync(contestId, includeHidden?, onlyUnapproved?)
Task<bool> ApproveWorkReviewAsync(reviewId, moderatorName)
Task<bool> HideWorkReviewAsync(reviewId)
Task<bool> AddAuthorResponseAsync(reviewId, response)
Task<bool> MarkReviewAsHelpfulAsync(reviewId)
Task<(int TotalReviews, decimal? AverageRating, int TopReviewsCount)> GetWorkReviewStatsAsync(contestId, workNumber)
```

## 🖥️ UI компоненты

### ContestDiscussion.razor
Страница обсуждения конкурса (`/contest/{ContestId}/discussion`):
- Форма добавления комментария
- Дерево комментариев с ответами в цепочку
- Лайки на комментарии
- Модерация (одобрение/скрытие) для модераторов
- Автоматическое одобрение для модераторов и выше

### WorkReviews.razor
Страница рецензий на работу (`/contest/{ContestId}/work/{WorkNumber}/reviews`):
- Форма добавления рецензии с оценкой
- Поля: достоинства, области для улучшения
- Статистика: общее количество, средняя оценка, полезные рецензии
- Модерация рецензий
- Автоматическое одобрение для модераторов

### CommentCard.razor
Компонент отображения одного комментария:
- Информация об авторе и роли
- Статус (ожидание модерации/одобрено)
- Текст комментария
- Кнопки: лайк, ответить
- Вложенные ответы
- Кнопки модерации (одобрить/скрыть)

### ReviewCard.razor
Компонент отображения рецензии:
- Заголовок и информация об авторе
- Рейтинг (звёзды)
- Основной текст
- Достоинства и области для улучшения
- Ответ автора работы (если есть)
- Кнопки: "полезно", модерация
- Статус одобрения

## 🌐 REST API

### Endpoints

#### Комментарии к конкурсу
- `GET /api/discussions/contests/{contestId}/comments` - Получить все комментарии
- `POST /api/discussions/contests/{contestId}/comments` - Добавить комментарий
- `POST /api/discussions/comments/{commentId}/like` - Лайк
- `POST /api/discussions/comments/{commentId}/approve` - Одобрить (модератор)
- `POST /api/discussions/comments/{commentId}/hide` - Скрыть (модератор)

#### Рецензии на работы
- `GET /api/discussions/contests/{contestId}/works/{workNumber}/reviews` - Получить рецензии
- `POST /api/discussions/contests/{contestId}/works/{workNumber}/reviews` - Добавить рецензию
- `GET /api/discussions/contests/{contestId}/works/{workNumber}/review-stats` - Статистика
- `POST /api/discussions/reviews/{reviewId}/helpful` - Отметить как полезную
- `POST /api/discussions/reviews/{reviewId}/approve` - Одобрить (модератор)
- `POST /api/discussions/reviews/{reviewId}/hide` - Скрыть (модератор)
- `POST /api/discussions/reviews/{reviewId}/author-response` - Ответ автора

## 🔐 Права доступа

| Действие | Reader | Author | Moderator | Admin |
|----------|--------|--------|-----------|-------|
| Читать комментарии | ✓ | ✓ | ✓ | ✓ |
| Писать комментарий | ✗ | ✓ | ✓ | ✓ |
| Лайк комментарий | ✓ | ✓ | ✓ | ✓ |
| Читать рецензии | ✓ | ✓ | ✓ | ✓ |
| Писать рецензию | ✗ | ✓ | ✓ | ✓ |
| Отметить рецензию полезной | ✓ | ✓ | ✓ | ✓ |
| Ответ на рецензию | ✗ | ✓ | ✓ | ✓ |
| Одобрить комментарий | ✗ | ✗ | ✓ | ✓ |
| Скрыть комментарий | ✗ | ✗ | ✓ | ✓ |
| Одобрить рецензию | ✗ | ✗ | ✓ | ✓ |
| Скрыть рецензию | ✗ | ✗ | ✓ | ✓ |

## 📊 База данных

### Таблица ContestComments
```sql
CREATE TABLE IF NOT EXISTS ContestComments(
    Id TEXT NOT NULL PRIMARY KEY,
    ContestId TEXT NOT NULL,
    AuthorName TEXT NOT NULL,
    AuthorRole INTEGER NOT NULL,
    Content TEXT NOT NULL,
    IsApproved INTEGER NOT NULL DEFAULT 0,
    IsHidden INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT,
    ApprovedAt TEXT,
    ApprovedBy TEXT,
    LikeCount INTEGER NOT NULL DEFAULT 0,
    ParentCommentId TEXT
);

CREATE INDEX idx_ContestComments_ContestId ON ContestComments(ContestId);
CREATE INDEX idx_ContestComments_CreatedAt ON ContestComments(CreatedAt);
CREATE INDEX idx_ContestComments_IsApproved ON ContestComments(IsApproved);
CREATE INDEX idx_ContestComments_ParentCommentId ON ContestComments(ParentCommentId);
```

### Таблица WorkReviews
```sql
CREATE TABLE IF NOT EXISTS WorkReviews(
    Id TEXT NOT NULL PRIMARY KEY,
    ContestId TEXT NOT NULL,
    WorkNumber INTEGER NOT NULL,
    WorkTitle TEXT,
    ReviewerName TEXT NOT NULL,
    ReviewerRole INTEGER NOT NULL,
    Title TEXT NOT NULL,
    Content TEXT NOT NULL,
    Rating INTEGER,
    Strengths TEXT,
    AreasForImprovement TEXT,
    IsApproved INTEGER NOT NULL DEFAULT 0,
    IsHidden INTEGER NOT NULL DEFAULT 0,
    IsPublic INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ApprovedAt TEXT,
    ApprovedBy TEXT,
    HelpfulCount INTEGER NOT NULL DEFAULT 0,
    AuthorResponse TEXT
);

CREATE INDEX idx_WorkReviews_ContestId ON WorkReviews(ContestId);
CREATE INDEX idx_WorkReviews_CreatedAt ON WorkReviews(CreatedAt);
CREATE INDEX idx_WorkReviews_IsApproved ON WorkReviews(IsApproved);
CREATE INDEX idx_WorkReviews_Contest_Work ON WorkReviews(ContestId, WorkNumber);
```

## 🔄 Workflow

### Создание и публикация комментария
1. Пользователь открывает страницу контеста (`/contest/{ContestId}/discussion`)
2. Пишет комментарий в форме
3. Нажимает "Добавить комментарий"
4. Комментарий автоматически одобрен если автор модератор/админ
5. Иначе ожидает модерации
6. Модератор видит неодобренные и может одобрить/скрыть
7. Одобренные видны всем

### Написание рецензии
1. Автор открывает страницу работы (`/contest/{ContestId}/work/{Number}/reviews`)
2. Заполняет форму: заголовок, текст, оценка, достоинства, улучшения
3. Отправляет рецензию
4. Рецензия ожидает модерации (кроме модераторов/админов)
5. Модератор одобряет и делает видимой публике
6. Другие читатели видят одобренные рецензии
7. Автор может ответить на рецензию

## 📈 Статистика

На странице работы отображается:
- **Всего рецензий**: количество одобренных рецензий
- **Средняя оценка**: среднее значение рейтинга (если есть)
- **Полезные рецензии**: количество с 3+ полезными голосами

## 🛠️ Интеграция

### В DependencyInjection
```csharp
services.AddScoped<DiscussionService>();
```

### В DbContext
```csharp
public DbSet<ContestComment> ContestComments { get; set; }
public DbSet<WorkReview> WorkReviews { get; set; }
```

### В NavMenu
```razor
<NavLink href="contest/contest-1/discussion">💬 Обсуждение</NavLink>
```

## 🧪 Тестирование

Все 35 существующих unit тестов продолжают работать.

### Рекомендуемые сценарии тестирования:
1. Добавить комментарий к конкурсу
2. Добавить ответ на комментарий
3. Лайк комментария
4. Написать рецензию на работу
5. Отметить рецензию как полезную
6. Модератор одобряет комментарий
7. Модератор одобряет рецензию
8. Модератор скрывает спам

## 📝 Примеры использования

### Добавить комментарий через API
```bash
curl -X POST http://localhost:5247/api/discussions/contests/contest-1/comments \
  -H "X-User-Name: author1" \
  -H "Content-Type: application/json" \
  -d '{
    "content": "Очень интересный конкурс!",
    "parentCommentId": null
  }'
```

### Добавить рецензию
```bash
curl -X POST http://localhost:5247/api/discussions/contests/contest-1/works/1/reviews \
  -H "X-User-Name: author2" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Прекрасная работа",
    "content": "Очень понравилась техника написания...",
    "rating": 5,
    "strengths": "Оригинальность, эмоциональность",
    "areasForImprovement": "Можно развить концовку"
  }'
```

## 📚 Дополнительно

- Все комментарии и рецензии хранят информацию об авторе
- Система поддерживает вложенные ответы на комментарии
- Система лайков позволяет отмечать полезные рецензии
- Модератор может скрывать спам не удаляя из БД
- Рецензии могут иметь ответ от автора работы
