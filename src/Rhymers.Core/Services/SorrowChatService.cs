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
