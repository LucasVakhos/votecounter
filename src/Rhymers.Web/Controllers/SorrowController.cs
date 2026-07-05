using Microsoft.AspNetCore.Mvc;
using Rhymers.Core.Models;
using Rhymers.Core.Services;
using Rhymers.Web.Services;

namespace Rhymers.Web.Controllers;

/// <summary>
/// API endpoints для чата "Страсти по рифме" - обсуждения переживаний участников конкурса
/// </summary>
[ApiController]
[Route("api/sorrow")]
public class SorrowController : ControllerBase
{
    private readonly SorrowChatService _sorrowService;
    private readonly AuthorizationWebService _authService;
    private readonly PersistenceService _persistenceService;

    public SorrowController(SorrowChatService sorrowService, AuthorizationWebService authService, PersistenceService persistenceService)
    {
        _sorrowService = sorrowService;
        _authService = authService;
        _persistenceService = persistenceService;
    }

    /// <summary>
    /// Получить все одобренные сообщения чата "Страсти по рифме" для конкурса
    /// </summary>
    /// <param name="contestId">ID конкурса</param>
    [HttpGet("contests/{contestId}/messages")]
    public async Task<ActionResult<IEnumerable<ContestSorrowMessage>>> GetSorrowMessages(string contestId)
    {
        var messages = await _sorrowService.GetApprovedSorrowMessagesAsync(contestId);
        return Ok(messages);
    }

    /// <summary>
    /// Получить ответы на конкретное сообщение
    /// </summary>
    [HttpGet("messages/{messageId}/replies")]
    public async Task<ActionResult<IEnumerable<ContestSorrowMessage>>> GetMessageReplies(string messageId)
    {
        var replies = await _sorrowService.GetMessageRepliesAsync(messageId);
        return Ok(replies);
    }

    /// <summary>
    /// Получить сообщение по ID
    /// </summary>
    [HttpGet("messages/{messageId}")]
    public async Task<ActionResult<ContestSorrowMessage>> GetSorrowMessage(string messageId)
    {
        var message = await _sorrowService.GetSorrowMessageAsync(messageId);
        if (message == null)
            return NotFound();
        return Ok(message);
    }

    /// <summary>
    /// Добавить новое сообщение в чат "Страсти по рифме"
    /// </summary>
    [HttpPost("contests/{contestId}/messages")]
    public async Task<ActionResult<ContestSorrowMessage>> AddSorrowMessage(string contestId, [FromBody] AddSorrowMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("Content cannot be empty");

        var currentUser = await _authService.GetCurrentUserAsync();
        if (currentUser == null)
            return Unauthorized();

        var message = new ContestSorrowMessage
        {
            ContestId = contestId,
            AuthorName = currentUser.DisplayName,
            AuthorRole = currentUser.Role,
            Content = request.Content,
            Type = request.Type ?? SorrowType.Reflection,
            ParentMessageId = request.ParentMessageId
        };

        var created = await _sorrowService.AddSorrowMessageAsync(contestId, message);
        return CreatedAtAction(nameof(GetSorrowMessage), new { messageId = created.Id }, created);
    }

    /// <summary>
    /// Добавить поддержку (empathy) к сообщению
    /// </summary>
    [HttpPost("messages/{messageId}/empathy")]
    public async Task<ActionResult<ContestSorrowMessage>> AddEmpathy(string messageId)
    {
        var message = await _sorrowService.AddEmpathyAsync(messageId);
        return Ok(message);
    }

    /// <summary>
    /// Одобрить сообщение (для модераторов)
    /// </summary>
    [HttpPost("messages/{messageId}/approve")]
    public async Task<ActionResult<ContestSorrowMessage>> ApproveSorrowMessage(string messageId)
    {
        var currentUser = await _authService.GetCurrentUserAsync();
        if (currentUser == null || currentUser.Role < UserRole.Moderator)
            return Forbid();

        var message = await _sorrowService.ApproveSorrowMessageAsync(messageId, currentUser.DisplayName);
        return Ok(message);
    }

    /// <summary>
    /// Скрыть сообщение (для модераторов - спам, оскорбления)
    /// </summary>
    [HttpPost("messages/{messageId}/hide")]
    public async Task<ActionResult<ContestSorrowMessage>> HideSorrowMessage(string messageId)
    {
        var currentUser = await _authService.GetCurrentUserAsync();
        if (currentUser == null || currentUser.Role < UserRole.Moderator)
            return Forbid();

        var message = await _sorrowService.HideSorrowMessageAsync(messageId);
        return Ok(message);
    }

    /// <summary>
    /// Получить статистику чата для конкурса
    /// </summary>
    [HttpGet("contests/{contestId}/stats")]
    public async Task<ActionResult<SorrowChatStatsDto>> GetChatStats(string contestId)
    {
        var stats = await _sorrowService.GetChatStatsAsync(contestId);
        return Ok(stats);
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
