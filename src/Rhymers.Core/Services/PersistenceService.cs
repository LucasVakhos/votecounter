using Rhymers.Core.Models;
using Rhymers.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Rhymers.Core.Services;

/// <summary>
/// Сервис для сохранения и загрузки данных из базы данных
/// </summary>
public sealed class PersistenceService
{
    private readonly RhymersDbContext _context;
    private readonly RoleAuthorizationService _authService;

    public PersistenceService(RhymersDbContext context, RoleAuthorizationService authService)
    {
        _context = context;
        _authService = authService;
    }

    /// <summary>
    /// Сохранить пользователя
    /// </summary>
    public async Task<User> SaveUserAsync(User user)
    {
        var existingUser = await _context.Users.FindAsync(user.Id);
        if (existingUser != null)
        {
            _context.Entry(existingUser).CurrentValues.SetValues(user);
        }
        else
        {
            _context.Users.Add(user);
        }

        await _context.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Получить пользователя по ID
    /// </summary>
    public async Task<User?> GetUserAsync(string userId)
    {
        return await _context.Users.FindAsync(userId);
    }

    /// <summary>
    /// Получить пользователя по имени
    /// </summary>
    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
    }

    /// <summary>
    /// Получить всех активных пользователей
    /// </summary>
    public async Task<List<User>> GetAllActiveUsersAsync()
    {
        return await _context.Users.Where(u => u.IsActive).ToListAsync();
    }

    /// <summary>
    /// Получить всех пользователей
    /// </summary>
    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _context.Users.ToListAsync();
    }

    /// <summary>
    /// Сохранить конкурс
    /// </summary>
    public async Task<Contest> SaveContestAsync(Contest contest)
    {
        var existingContest = await _context.Contests.FindAsync(contest.Id);
        if (existingContest != null)
        {
            _context.Entry(existingContest).CurrentValues.SetValues(contest);
        }
        else
        {
            _context.Contests.Add(contest);
        }

        await _context.SaveChangesAsync();
        return contest;
    }

    /// <summary>
    /// Получить конкурс по ID
    /// </summary>
    public async Task<Contest?> GetContestAsync(string contestId)
    {
        return await _context.Contests.FindAsync(contestId);
    }

    /// <summary>
    /// Получить все конкурсы
    /// </summary>
    public async Task<List<Contest>> GetAllContestsAsync()
    {
        return await _context.Contests.OrderByDescending(c => c.CreatedAt).ToListAsync();
    }

    /// <summary>
    /// Сохранить работу (nested в WorkSubmission)
    /// </summary>
    public async Task<WorkSubmission> SaveWorkAsync(ContestWork work, string submissionId)
    {
        var submission = await _context.Submissions.FindAsync(submissionId);
        if (submission != null)
        {
            submission.Work = work;
            await _context.SaveChangesAsync();
        }

        return submission ?? throw new InvalidOperationException($"Submission {submissionId} not found");
    }

    /// <summary>
    /// Сохранить подачу (submission)
    /// </summary>
    public async Task<WorkSubmission> SaveSubmissionAsync(WorkSubmission submission)
    {
        var existingSubmission = await _context.Submissions.FindAsync(submission.Id);
        if (existingSubmission != null)
        {
            _context.Entry(existingSubmission).CurrentValues.SetValues(submission);
        }
        else
        {
            _context.Submissions.Add(submission);
        }

        await _context.SaveChangesAsync();
        return submission;
    }

    /// <summary>
    /// Получить подачу по ID
    /// </summary>
    public async Task<WorkSubmission?> GetSubmissionAsync(string submissionId)
    {
        return await _context.Submissions.FindAsync(submissionId);
    }

    /// <summary>
    /// Получить все подачи конкурса
    /// </summary>
    public async Task<List<WorkSubmission>> GetSubmissionsByContestAsync(string contestId)
    {
        return await _context.Submissions
            .Where(s => s.ContestId == contestId)
            .OrderByDescending(s => s.SubmittedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Очистить всю базу данных (только для тестов/демо)
    /// </summary>
    public async Task ClearAllAsync()
    {
        _context.Users.RemoveRange(_context.Users);
        _context.Contests.RemoveRange(_context.Contests);
        _context.Submissions.RemoveRange(_context.Submissions);
        _context.Topics.RemoveRange(_context.Topics);
        _context.TopicKinds.RemoveRange(_context.TopicKinds);
        _context.Voters.RemoveRange(_context.Voters);

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Инициализировать БД (создать таблицы, добавить демо данные)
    /// </summary>
    public async Task InitializeDatabaseAsync()
    {
        // Создать таблицы
        await _context.Database.EnsureCreatedAsync();
        await EnsureSchemaExtensionsAsync();

        // Проверить есть ли уже конкурсы
        var contestCount = await _context.Contests.CountAsync();
        if (contestCount > 0)
            return; // БД уже инициализирована

        // Добавить демо конкурсы только если их нет
        var contests = new List<Contest>
        {
            new()
            {
                Id = "contest-1",
                Number = "001",
                Name = "Конкурс Рифмовки - Июль 2026",
                IsActive = true,
                StartedAt = DateTime.Now.AddDays(-7),
                ClosedAt = null
            },
            new()
            {
                Id = "contest-2",
                Number = "002",
                Name = "Конкурс Рифмовки - Август 2026",
                IsActive = false,
                StartedAt = DateTime.Now.AddDays(-30),
                ClosedAt = DateTime.Now.AddDays(-5)
            }
        };

        await _context.Contests.AddRangeAsync(contests);
        await _context.SaveChangesAsync();

        // Добавить демо пользователей с реальными паролями
        var userCount = await _context.Users.CountAsync();
        if (userCount == 0)
        {
            try
            {
                _authService.RegisterUser("reader1", "Читатель Первый", "reader1@test.ru", "password123", UserRole.Reader);
                _authService.RegisterUser("author1", "Автор Первый", "author1@test.ru", "password123", UserRole.Author);
                _authService.RegisterUser("moderator1", "Модератор Первый", "moderator1@test.ru", "password123", UserRole.Moderator);
                _authService.RegisterUser("admin1", "Администратор", "admin1@test.ru", "password123", UserRole.Admin);
            }
            catch
            {
                // Пользователи уже существуют, игнорируем ошибку
            }
        }
    }

    private async Task EnsureSchemaExtensionsAsync()
    {
        await EnsureColumnAsync("Contests", "MaxTopicsCount", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Contests", "AutoStageSwitchEnabled", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Contests", "TopicReceptionEndsAt", "TEXT NULL");
        await EnsureColumnAsync("Contests", "WorkReceptionEndsAt", "TEXT NULL");
        await EnsureColumnAsync("Contests", "VotingOpenEndsAt", "TEXT NULL");
        await EnsureColumnAsync("Contests", "VotingClosedEndsAt", "TEXT NULL");

        await _context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ContestTopics(
                ContestId TEXT NOT NULL,
                Number INTEGER NOT NULL,
                Title TEXT NOT NULL,
                TopicKindId INTEGER NULL,
                ProposedBy TEXT NOT NULL DEFAULT '',
                IsWinnerTopic INTEGER NOT NULL DEFAULT 0,
                SubmittedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY(ContestId, Number)
            );");

        await _context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS TopicKinds(
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                SortNo INTEGER NOT NULL DEFAULT 0
            );");

        await EnsureColumnAsync("ContestTopics", "TopicKindId", "INTEGER NULL");
        await EnsureColumnAsync("ContestTopics", "ProposedBy", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync("ContestTopics", "IsWinnerTopic", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("ContestTopics", "SubmittedAt", "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP");

        await _context.Database.ExecuteSqlRawAsync(@"
            INSERT OR IGNORE INTO TopicKinds(Id, Name, SortNo) VALUES(1, 'Строка', 1);
            INSERT OR IGNORE INTO TopicKinds(Id, Name, SortNo) VALUES(2, 'Картина', 2);
            INSERT OR IGNORE INTO TopicKinds(Id, Name, SortNo) VALUES(3, 'Тема', 3);
        ");
    }

    private async Task EnsureColumnAsync(string tableName, string columnName, string columnDefinition)
    {
        if (!IsSafeSqlIdentifier(tableName) || !IsSafeSqlIdentifier(columnName))
            throw new InvalidOperationException("Invalid SQL identifier for schema update.");

        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({tableName});";

        var exists = false;
        await using (var reader = await check.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (exists)
            return;

        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        await alter.ExecuteNonQueryAsync();
    }

    private static bool IsSafeSqlIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.All(ch => char.IsLetterOrDigit(ch) || ch == '_');
    }
}
