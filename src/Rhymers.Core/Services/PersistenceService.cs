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
        _context.ContestVotes.RemoveRange(_context.ContestVotes);
        _context.ContestStageTimelineEvents.RemoveRange(_context.ContestStageTimelineEvents);
        _context.UserSanctionNotifications.RemoveRange(_context.UserSanctionNotifications);
        _context.UserSanctionDispatchAudits.RemoveRange(_context.UserSanctionDispatchAudits);
        _context.HallOfFameEntries.RemoveRange(_context.HallOfFameEntries);
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
        await EnsureColumnAsync("Contests", "TopicReceptionSwitchDayOfWeek", "INTEGER NULL");
        await EnsureColumnAsync("Contests", "TopicReceptionSwitchTime", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync("Contests", "WorkReceptionSwitchDayOfWeek", "INTEGER NULL");
        await EnsureColumnAsync("Contests", "WorkReceptionSwitchTime", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync("Contests", "VotingOpenSwitchDayOfWeek", "INTEGER NULL");
        await EnsureColumnAsync("Contests", "VotingOpenSwitchTime", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync("Contests", "VotingClosedSwitchDayOfWeek", "INTEGER NULL");
        await EnsureColumnAsync("Contests", "VotingClosedSwitchTime", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync("Contests", "RollbackWindowHours", "INTEGER NOT NULL DEFAULT 5");
        await EnsureColumnAsync("Contests", "LastManualRollbackAt", "TEXT NULL");
        await EnsureColumnAsync("Contests", "AutoTopicAssignmentEnabled", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Contests", "AutoTopicAssignmentTargetCount", "INTEGER NOT NULL DEFAULT 5");
        await EnsureColumnAsync("Contests", "AutoFairVotingEnabled", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Contests", "AutoAdminAverageVotingOnCloseEnabled", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("Contests", "UnfairVotingDetectionThreshold", "REAL NOT NULL DEFAULT 1.5");
        await EnsureColumnAsync("Contests", "UnfairVotingMinVotesForAnalysis", "INTEGER NOT NULL DEFAULT 5");
        await EnsureColumnAsync("Contests", "UnfairVotingSelfVoteRiskWeight", "REAL NOT NULL DEFAULT 1.5");
        await EnsureColumnAsync("Contests", "UnfairVotingExtremesRiskWeight", "REAL NOT NULL DEFAULT 1.0");
        await EnsureColumnAsync("Contests", "UnfairVotingFavoritismRiskWeight", "REAL NOT NULL DEFAULT 1.2");
        await EnsureColumnAsync("Contests", "WinnersPraiseText", "TEXT NOT NULL DEFAULT ''");

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

        await _context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ContestVotes(
                ContestId TEXT NOT NULL,
                SubmissionId TEXT NOT NULL,
                VoterUserId TEXT NOT NULL,
                VoterUsername TEXT NOT NULL,
                Score INTEGER NOT NULL,
                Comment TEXT NOT NULL DEFAULT '',
                UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY(ContestId, SubmissionId, VoterUserId)
            );");

        await _context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ContestStageTimelineEvents(
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                ContestId TEXT NOT NULL,
                EventType TEXT NOT NULL,
                StageFrom INTEGER NOT NULL,
                StageTo INTEGER NOT NULL,
                CreatedBy TEXT NOT NULL DEFAULT '',
                Message TEXT NOT NULL DEFAULT '',
                AlarmKey TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );");

        await _context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS UserSanctionNotifications(
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                Username TEXT NOT NULL,
                Title TEXT NOT NULL,
                Message TEXT NOT NULL,
                IsRead INTEGER NOT NULL DEFAULT 0,
                CreatedBy TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ReadAt TEXT NULL
            );");

        await _context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS UserSanctionDispatchAudits(
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                ContestId TEXT NOT NULL,
                RecipientUserId TEXT NOT NULL,
                RecipientUsername TEXT NOT NULL,
                Reason TEXT NOT NULL,
                RiskScore REAL NOT NULL DEFAULT 0,
                SentBy TEXT NOT NULL DEFAULT '',
                TemplateText TEXT NOT NULL DEFAULT '',
                RenderedMessage TEXT NOT NULL DEFAULT '',
                SentAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );");

        await EnsureColumnAsync("ContestStageTimelineEvents", "AlarmKey", "TEXT NOT NULL DEFAULT ''");

        await EnsureColumnAsync("ContestTopics", "TopicKindId", "INTEGER NULL");
        await EnsureColumnAsync("ContestTopics", "ProposedBy", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync("ContestTopics", "IsWinnerTopic", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync("ContestTopics", "SubmittedAt", "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP");

        await _context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS HallOfFameEntries(
                Id TEXT NOT NULL PRIMARY KEY,
                ContestId TEXT NOT NULL,
                ContestNumber TEXT NOT NULL,
                ContestName TEXT NOT NULL,
                Place INTEGER NOT NULL,
                PlaceTitle TEXT NOT NULL,
                WorkNumber INTEGER NOT NULL,
                Topic TEXT NOT NULL,
                Author TEXT NOT NULL,
                TotalScore INTEGER NOT NULL,
                AverageScore REAL NOT NULL,
                VotesCount INTEGER NOT NULL,
                AuthorPhotoUrl TEXT,
                Description TEXT,
                AddedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ContestDate TEXT NOT NULL
            );");

        await _context.Database.ExecuteSqlRawAsync(@"
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
            );");

        await _context.Database.ExecuteSqlRawAsync(@"
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
            );");

        await _context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ContestSorrowMessages(
                Id TEXT NOT NULL PRIMARY KEY,
                ContestId TEXT NOT NULL,
                AuthorName TEXT NOT NULL,
                AuthorRole INTEGER NOT NULL,
                Content TEXT NOT NULL,
                IsApproved INTEGER NOT NULL DEFAULT 1,
                IsHidden INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ApprovedAt TEXT,
                ApprovedBy TEXT,
                EmpathyCount INTEGER NOT NULL DEFAULT 0,
                ParentMessageId TEXT,
                Type INTEGER NOT NULL DEFAULT 0
            );");

        await _context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS UserViolations(
                Id TEXT NOT NULL PRIMARY KEY,
                ContestId TEXT NOT NULL,
                UserName TEXT NOT NULL,
                MessageId TEXT NOT NULL,
                Type INTEGER NOT NULL DEFAULT 0,
                Details TEXT,
                ModeratorName TEXT NOT NULL,
                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                IsCleared INTEGER NOT NULL DEFAULT 0,
                ClearedAt TEXT,
                ClearedByModerator TEXT,
                Sanction INTEGER NOT NULL DEFAULT 0,
                SanctionAdminName TEXT,
                SanctionAppliedAt TEXT,
                SanctionExpiredAt TEXT,
                SanctionReason TEXT
            );");

        await _context.Database.ExecuteSqlRawAsync(@"
            INSERT OR IGNORE INTO TopicKinds(Id, Name, SortNo) VALUES(1, 'Строка', 1);
            INSERT OR IGNORE INTO TopicKinds(Id, Name, SortNo) VALUES(2, 'Картина', 2);
            INSERT OR IGNORE INTO TopicKinds(Id, Name, SortNo) VALUES(3, 'Тема', 3);
        ");

        await _context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS AuditLogs(
                Id TEXT NOT NULL PRIMARY KEY,
                Action INTEGER NOT NULL DEFAULT 0,
                ActorName TEXT NOT NULL,
                ActorRole INTEGER NOT NULL DEFAULT 0,
                TargetUserName TEXT NOT NULL,
                ContestId TEXT,
                RelatedEntityId TEXT,
                Details TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );");

        await _context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS UserNotifications(
                Id TEXT NOT NULL PRIMARY KEY,
                UserName TEXT NOT NULL,
                Type INTEGER NOT NULL DEFAULT 0,
                Title TEXT NOT NULL DEFAULT '',
                Message TEXT NOT NULL DEFAULT '',
                IsRead INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ReadAt TEXT
            );");

        await _context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS SanctionAppeals(
                Id TEXT NOT NULL PRIMARY KEY,
                ViolationId TEXT NOT NULL,
                ContestId TEXT NOT NULL,
                UserName TEXT NOT NULL,
                Reason TEXT NOT NULL DEFAULT '',
                Status INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ReviewedAt TEXT,
                ReviewedByAdmin TEXT,
                AdminComment TEXT
            );");
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
