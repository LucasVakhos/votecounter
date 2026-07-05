using Microsoft.AspNetCore.Mvc;
using Rhymers.Core.Models;
using Rhymers.Web.Services;

namespace Rhymers.Web.Controllers;

/// <summary>
/// API endpoints для приватных сообщений между друзьями (STAGE29)
/// </summary>
[ApiController]
[Route("api/messages")]
public class DirectMessagesController : ControllerBase
{
    private readonly DirectMessageWebService _directMessageService;
    private readonly AttachmentWebService _attachmentService;

    public DirectMessagesController(DirectMessageWebService directMessageService, AttachmentWebService attachmentService)
    {
        _directMessageService = directMessageService;
        _attachmentService = attachmentService;
    }

    /// <summary>
    /// Отправить приватное сообщение
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UserDirectMessage>> SendMessage([FromBody] SendMessageRequest request)
    {
        if (string.IsNullOrEmpty(request.FromUserId) || string.IsNullOrEmpty(request.ToUserId))
            return BadRequest("FromUserId и ToUserId обязательны");

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("Содержание сообщения не может быть пустым");

        try
        {
            var message = await _directMessageService.SendMessageAsync(request.FromUserId, request.ToUserId, request.Content);
            return CreatedAtAction(nameof(GetMessage), new { messageId = message.Id }, message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Получить диалог между двумя пользователями
    /// </summary>
    [HttpGet("conversation")]
    public async Task<ActionResult<List<(UserDirectMessage Message, User Sender, User Recipient)>>> GetConversation(
        [FromQuery] string userId1, [FromQuery] string userId2, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        if (string.IsNullOrEmpty(userId1) || string.IsNullOrEmpty(userId2))
            return BadRequest("userId1 и userId2 обязательны");

        var messages = await _directMessageService.GetConversationWithDetailsAsync(userId1, userId2, skip, take);
        return Ok(messages);
    }

    /// <summary>
    /// Получить список диалогов пользователя
    /// </summary>
    [HttpGet("dialogs")]
    public async Task<ActionResult<List<DialogInfo>>> GetDialogs([FromQuery] string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("userId обязателен");

        var dialogs = await _directMessageService.GetDialogsAsync(userId);
        return Ok(dialogs);
    }

    /// <summary>
    /// Получить количество непрочитанных сообщений
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount([FromQuery] string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("userId обязателен");

        var count = await _directMessageService.GetUnreadCountAsync(userId);
        return Ok(count);
    }

    /// <summary>
    /// Получить сообщение по ID
    /// </summary>
    [HttpGet("{messageId}")]
    public async Task<ActionResult<UserDirectMessage>> GetMessage(string messageId, [FromQuery] string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("userId обязателен");

        try
        {
            var message = await _directMessageService.GetMessageAsync(messageId, userId);
            return Ok(message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Сообщение не найдено");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    /// <summary>
    /// Отметить сообщение как прочитанное
    /// </summary>
    [HttpPut("{messageId}/read")]
    public async Task<IActionResult> MarkAsRead(string messageId, [FromQuery] string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("userId обязателен");

        try
        {
            await _directMessageService.MarkAsReadAsync(messageId, userId);
            return Ok();
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Сообщение не найдено");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    /// <summary>
    /// Отметить все сообщения в диалоге как прочитанные
    /// </summary>
    [HttpPut("conversation/read-all")]
    public async Task<IActionResult> MarkConversationAsRead([FromQuery] string userId, [FromQuery] string otherUserId)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(otherUserId))
            return BadRequest("userId и otherUserId обязательны");

        await _directMessageService.MarkConversationAsReadAsync(userId, otherUserId);
        return Ok();
    }

    /// <summary>
    /// Редактировать сообщение
    /// </summary>
    [HttpPut("{messageId}")]
    public async Task<ActionResult<UserDirectMessage>> EditMessage(string messageId, [FromBody] EditMessageRequest request, [FromQuery] string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("userId обязателен");

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("Содержание сообщения не может быть пустым");

        try
        {
            var message = await _directMessageService.EditMessageAsync(messageId, userId, request.Content);
            return Ok(message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Сообщение не найдено");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Удалить сообщение
    /// </summary>
    [HttpDelete("{messageId}")]
    public async Task<IActionResult> DeleteMessage(string messageId, [FromQuery] string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("userId обязателен");

        try
        {
            await _directMessageService.DeleteMessageAsync(messageId, userId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Сообщение не найдено");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    /// <summary>
    /// Загрузить картинку к сообщению
    /// </summary>
    [HttpPost("{messageId}/upload-image")]
    public async Task<ActionResult<MessageAttachment>> UploadImage(string messageId, IFormFile file, [FromQuery] string messageType)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Файл не выбран");

        try
        {
            var attachmentType = messageType == "sorrow" 
                ? MessageAttachmentType.SorrowChat 
                : MessageAttachmentType.DirectMessage;

            var attachment = await _attachmentService.UploadImageAsync(messageId, file, attachmentType);
            return CreatedAtAction(nameof(GetMessage), new { messageId = messageId }, attachment);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Добавить смайлик к сообщению
    /// </summary>
    [HttpPost("{messageId}/add-emoji")]
    public async Task<ActionResult<MessageAttachment>> AddEmoji(string messageId, [FromBody] AddEmojiRequest request, [FromQuery] string messageType)
    {
        if (string.IsNullOrEmpty(request.EmojiCode))
            return BadRequest("EmojiCode обязателен");

        try
        {
            var attachmentType = messageType == "sorrow" 
                ? MessageAttachmentType.SorrowChat 
                : MessageAttachmentType.DirectMessage;

            var attachment = await _attachmentService.AddEmojiAsync(messageId, request.EmojiCode, attachmentType);
            return CreatedAtAction(nameof(GetMessage), new { messageId = messageId }, attachment);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Получить вложения сообщения
    /// </summary>
    [HttpGet("{messageId}/attachments")]
    public async Task<ActionResult<List<MessageAttachment>>> GetAttachments(string messageId)
    {
        var attachments = await _attachmentService.GetAttachmentsAsync(messageId);
        return Ok(attachments);
    }

    /// <summary>
    /// Получить список доступных смайликов
    /// </summary>
    [HttpGet("emojis")]
    public ActionResult<Dictionary<string, string>> GetEmojiDictionary()
    {
        var emojis = _attachmentService.GetEmojiDictionary();
        return Ok(emojis);
    }

    /// <summary>
    /// Очистить диалог
    /// </summary>
    [HttpDelete("conversation/clear")]
    public async Task<IActionResult> ClearConversation([FromQuery] string userId, [FromQuery] string otherUserId)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(otherUserId))
            return BadRequest("userId и otherUserId обязательны");

        await _directMessageService.ClearConversationAsync(userId, otherUserId);
        return NoContent();
    }
}

/// <summary>Запрос на отправку сообщения</summary>
public class SendMessageRequest
{
    public string FromUserId { get; set; } = string.Empty;
    public string ToUserId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

/// <summary>Запрос на редактирование сообщения</summary>
public class EditMessageRequest
{
    public string Content { get; set; } = string.Empty;
}

/// <summary>Запрос на добавление смайлика</summary>
public class AddEmojiRequest
{
    public string EmojiCode { get; set; } = string.Empty;
}
