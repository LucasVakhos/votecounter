using Rhymers.Core.Data;
using Rhymers.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Rhymers.Core.Services;

/// <summary>
/// Сервис для управления чатом "Страсти по рифме" - обсуждением переживаний участников
/// </summary>
public sealed class SorrowChatService
{
    private readonly RhymersDbContext _context;

    public SorrowChatService(RhymersDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Добавить новое сообщение о переживаниях
    /// </summary>
    public async Task<ContestSorrowMessage> AddSorrowMessageAsync(string contestId, ContestSorrowMessage message)
    {
        if (string.IsNullOrEmpty(contestId))
            throw new ArgumentException("Contest ID cannot be empty", nameof(contestId));

        message.Id = Guid.NewGuid().ToString("N");
        message.ContestId = contestId;
        message.CreatedAt = DateTime.Now;
        message.UpdatedAt = DateTime.Now;

        _context.SorrowMessages.Add(message);
        await _context.SaveChangesAsync();
        return message;
    }

    /// <summary>
    /// Получить все одобренные сообщения чата "Страсти по рифме" для конкурса
    /// </summary>
    public async Task<List<ContestSorrowMessage>> GetApprovedSorrowMessagesAsync(string contestId)
    {
        return await _context.SorrowMessages
            .Where(m => m.ContestId == contestId && 
                        m.IsApproved && 
                        !m.IsHidden &&
                        m.ParentMessageId == null)  // Только корневые сообщения
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Получить ответы на конкретное сообщение (поддержки/развитие темы)
    /// </summary>
    public async Task<List<ContestSorrowMessage>> GetMessageRepliesAsync(string parentMessageId)
    {
        return await _context.SorrowMessages
            .Where(m => m.ParentMessageId == parentMessageId && 
                        m.IsApproved && 
                        !m.IsHidden)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Получить сообщение по ID
    /// </summary>
    public async Task<ContestSorrowMessage?> GetSorrowMessageAsync(string messageId)
    {
        return await _context.SorrowMessages.FirstOrDefaultAsync(m => m.Id == messageId);
    }

    /// <summary>
    /// Одобрить сообщение (модератор)
    /// </summary>
    public async Task<ContestSorrowMessage> ApproveSorrowMessageAsync(string messageId, string moderatorName)
    {
        var message = await GetSorrowMessageAsync(messageId);
        if (message == null)
            throw new InvalidOperationException($"Message {messageId} not found");

        message.IsApproved = true;
        message.ApprovedAt = DateTime.Now;
        message.ApprovedBy = moderatorName;
        message.UpdatedAt = DateTime.Now;

        _context.SorrowMessages.Update(message);
        await _context.SaveChangesAsync();
        return message;
    }

    /// <summary>
    /// Скрыть сообщение (если оскорбление, спам и т.д.)
    /// </summary>
    public async Task<ContestSorrowMessage> HideSorrowMessageAsync(string messageId)
    {
        var message = await GetSorrowMessageAsync(messageId);
        if (message == null)
            throw new InvalidOperationException($"Message {messageId} not found");

        message.IsHidden = true;
        message.UpdatedAt = DateTime.Now;

        _context.SorrowMessages.Update(message);
        await _context.SaveChangesAsync();
        return message;
    }

    /// <summary>
    /// Добавить "поддержку" (empathy) к сообщению
    /// </summary>
    public async Task<ContestSorrowMessage> AddEmpathyAsync(string messageId)
    {
        var message = await GetSorrowMessageAsync(messageId);
        if (message == null)
            throw new InvalidOperationException($"Message {messageId} not found");

        message.EmpathyCount++;
        message.UpdatedAt = DateTime.Now;

        _context.SorrowMessages.Update(message);
        await _context.SaveChangesAsync();
        return message;
    }

    /// <summary>
    /// Получить статистику чата для конкурса
    /// </summary>
    public async Task<SorrowChatStatsDto> GetChatStatsAsync(string contestId)
    {
        var allMessages = await _context.SorrowMessages
            .Where(m => m.ContestId == contestId)
            .ToListAsync();

        return new SorrowChatStatsDto
        {
            TotalMessages = allMessages.Count,
            ApprovedMessages = allMessages.Count(m => m.IsApproved && !m.IsHidden),
            HiddenMessages = allMessages.Count(m => m.IsHidden),
            RootMessages = allMessages.Count(m => m.ParentMessageId == null && m.IsApproved && !m.IsHidden),
            TotalEmpathy = allMessages.Sum(m => m.EmpathyCount),
            MessagesByType = allMessages
                .Where(m => m.IsApproved && !m.IsHidden)
                .GroupBy(m => m.Type)
                .ToDictionary(g => g.Key.ToString(), g => g.Count())
        };
    }

    // ==================== HamFilter: Система контроля за хамством ====================

    /// <summary>
    /// Отметить сообщение как хамство (нарушение)
    /// </summary>
    public async Task<UserViolation> MarkAsViolationAsync(string contestId, string messageId, string moderatorName, ViolationType type = ViolationType.Rudeness, string? details = null)
    {
        var message = await GetSorrowMessageAsync(messageId);
        if (message == null)
            throw new InvalidOperationException($"Message {messageId} not found");

        var violation = new UserViolation
        {
            Id = Guid.NewGuid().ToString("N"),
            ContestId = contestId,
            UserName = message.AuthorName,
            MessageId = messageId,
            Type = type,
            Details = details,
            ModeratorName = moderatorName,
            CreatedAt = DateTime.UtcNow,
            IsCleared = false
        };

        _context.UserViolations.Add(violation);
        await _context.SaveChangesAsync();
        return violation;
    }

    /// <summary>
    /// Получить статистику нарушений пользователя
    /// </summary>
    public async Task<UserViolationStats> GetUserViolationStatsAsync(string contestId, string userName)
    {
        var violations = await _context.UserViolations
            .Where(v => v.ContestId == contestId && v.UserName == userName)
            .ToListAsync();

        var activeViolations = violations.Count(v => !v.IsCleared);
        const int MaxViolationsBeforeBlock = 3; // Блокировка после 3-х нарушений

        var violationsByType = violations
            .Where(v => !v.IsCleared)
            .GroupBy(v => v.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        return new UserViolationStats
        {
            UserName = userName,
            TotalViolations = violations.Count,
            ActiveViolations = activeViolations,
            LastViolationAt = violations.OrderByDescending(v => v.CreatedAt).FirstOrDefault()?.CreatedAt,
            IsBlocked = activeViolations >= MaxViolationsBeforeBlock,
            BlockReason = activeViolations >= MaxViolationsBeforeBlock 
                ? $"⛔ Пользователь заблокирован на этапе модерации. Нарушений: {activeViolations}/3"
                : null,
            ViolationsByType = violationsByType
        };
    }

    /// <summary>
    /// Получить все нарушения в контесте
    /// </summary>
    public async Task<List<UserViolation>> GetContestViolationsAsync(string contestId, bool onlyActive = false)
    {
        var query = _context.UserViolations.Where(v => v.ContestId == contestId);
        
        if (onlyActive)
            query = query.Where(v => !v.IsCleared);

        return await query
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Снять предупреждение (реабилитация пользователя)
    /// </summary>
    public async Task<UserViolation> ClearViolationAsync(string violationId, string moderatorName)
    {
        var violation = await _context.UserViolations.FindAsync(violationId);
        if (violation == null)
            throw new InvalidOperationException($"Violation {violationId} not found");

        violation.IsCleared = true;
        violation.ClearedAt = DateTime.UtcNow;
        violation.ClearedByModerator = moderatorName;

        _context.UserViolations.Update(violation);
        await _context.SaveChangesAsync();
        return violation;
    }
}

/// <summary>
/// DTO для статистики чата "Страсти по рифме"
/// </summary>
public class SorrowChatStatsDto
{
    public int TotalMessages { get; set; }
    public int ApprovedMessages { get; set; }
    public int HiddenMessages { get; set; }
    public int RootMessages { get; set; }
    public int TotalEmpathy { get; set; }
    public Dictionary<string, int> MessagesByType { get; set; } = new();
}
