using Rhymers.Core.Models;
using Rhymers.Core.Services;

namespace Rhymers.Web.Services;

/// <summary>
/// Веб-сервис для работы с чатом "Страсти по рифме" в Blazor приложении
/// </summary>
public class SorrowChatWebService
{
    private readonly SorrowChatService _sorrowService;

    public SorrowChatWebService(SorrowChatService sorrowService)
    {
        _sorrowService = sorrowService;
    }

    /// <summary>
    /// Получить все одобренные сообщения для конкурса
    /// </summary>
    public async Task<List<ContestSorrowMessage>> GetApprovedSorrowMessagesAsync(string contestId)
    {
        var messages = await _sorrowService.GetApprovedSorrowMessagesAsync(contestId);
        return await Task.FromResult(messages);
    }

    /// <summary>
    /// Получить ответы на сообщение
    /// </summary>
    public async Task<List<ContestSorrowMessage>> GetMessageRepliesAsync(string parentMessageId)
    {
        var replies = await _sorrowService.GetMessageRepliesAsync(parentMessageId);
        return await Task.FromResult(replies);
    }

    /// <summary>
    /// Получить сообщение по ID
    /// </summary>
    public async Task<ContestSorrowMessage?> GetSorrowMessageAsync(string messageId)
    {
        var message = await _sorrowService.GetSorrowMessageAsync(messageId);
        return await Task.FromResult(message);
    }

    /// <summary>
    /// Добавить новое сообщение
    /// </summary>
    public async Task<ContestSorrowMessage> AddSorrowMessageAsync(string contestId, AddSorrowMessageRequest request)
    {
        var message = new ContestSorrowMessage
        {
            ContestId = contestId,
            Content = request.Content,
            Type = request.Type ?? SorrowType.Reflection,
            ParentMessageId = request.ParentMessageId
        };

        var created = await _sorrowService.AddSorrowMessageAsync(contestId, message);
        return await Task.FromResult(created);
    }

    /// <summary>
    /// Добавить поддержку (empathy) к сообщению
    /// </summary>
    public async Task<ContestSorrowMessage> AddEmpathyAsync(string messageId)
    {
        var message = await _sorrowService.AddEmpathyAsync(messageId);
        return await Task.FromResult(message);
    }

    /// <summary>
    /// Одобрить сообщение (модератор)
    /// </summary>
    public async Task<ContestSorrowMessage> ApproveSorrowMessageAsync(string messageId)
    {
        var message = await _sorrowService.ApproveSorrowMessageAsync(messageId, "Moderator");
        return await Task.FromResult(message);
    }

    /// <summary>
    /// Скрыть сообщение (модератор)
    /// </summary>
    public async Task<ContestSorrowMessage> HideSorrowMessageAsync(string messageId)
    {
        var message = await _sorrowService.HideSorrowMessageAsync(messageId);
        return await Task.FromResult(message);
    }

    /// <summary>
    /// Получить статистику чата
    /// </summary>
    public async Task<SorrowChatStatsDto> GetChatStatsAsync(string contestId)
    {
        var stats = await _sorrowService.GetChatStatsAsync(contestId);
        return await Task.FromResult(stats);
    }
}

/// <summary>
/// Request model для добавления сообщения
/// </summary>
public class AddSorrowMessageRequest
{
    public string Content { get; set; } = string.Empty;
    public SorrowType? Type { get; set; }
    public string? ParentMessageId { get; set; }
}
