using Microsoft.AspNetCore.Components.Forms;
using Rhymers.Core.Models;
using Rhymers.Core.Services;

namespace Rhymers.Web.Services;

/// <summary>
/// Web-сервис управления вложениями (обертка для API и Blazor)
/// </summary>
public sealed class AttachmentWebService
{
    private readonly AttachmentService _attachmentService;
    private readonly IWebHostEnvironment _environment;

    public AttachmentWebService(AttachmentService attachmentService, IWebHostEnvironment environment)
    {
        _attachmentService = attachmentService;
        _environment = environment;
    }

    /// <summary>
    /// Загрузить картинку и вернуть информацию о вложении
    /// </summary>
    public async Task<MessageAttachment> UploadImageAsync(string messageId, IFormFile file, MessageAttachmentType messageType)
    {
        if (file.Length == 0)
            throw new ArgumentException("Файл пуст");

        // Проверить тип файла
        var allowedMimes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (!allowedMimes.Contains(file.ContentType))
            throw new ArgumentException("Недопустимый формат изображения. Допускаются JPEG, PNG, GIF, WebP");

        // Проверить размер (макс 5MB)
        const long maxSize = 5 * 1024 * 1024;
        if (file.Length > maxSize)
            throw new ArgumentException("Размер файла превышает 5MB");

        // Создать папку для вложений если её нет
        var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "attachments");
        if (!Directory.Exists(uploadsPath))
            Directory.CreateDirectory(uploadsPath);

        // Сохранить файл
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsPath, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Добавить в БД
        var fileUrl = $"/uploads/attachments/{fileName}";
        return await _attachmentService.AddAttachmentAsync(
            messageId,
            messageType,
            AttachmentType.Image,
            fileUrl,
            file.FileName,
            file.Length,
            file.ContentType,
            Path.GetFileNameWithoutExtension(file.FileName)
        );
    }

    /// <summary>
    /// Загрузить картинку из Blazor InputFile и вернуть информацию о вложении
    /// </summary>
    public async Task<MessageAttachment> UploadImageAsync(string messageId, IBrowserFile file, MessageAttachmentType messageType)
    {
        if (file.Size == 0)
            throw new ArgumentException("Файл пуст");

        var allowedMimes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (!allowedMimes.Contains(file.ContentType))
            throw new ArgumentException("Недопустимый формат изображения. Допускаются JPEG, PNG, GIF, WebP");

        const long maxSize = 5 * 1024 * 1024;
        if (file.Size > maxSize)
            throw new ArgumentException("Размер файла превышает 5MB");

        var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "attachments");
        if (!Directory.Exists(uploadsPath))
            Directory.CreateDirectory(uploadsPath);

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.Name)}";
        var filePath = Path.Combine(uploadsPath, fileName);

        await using (var sourceStream = file.OpenReadStream(maxSize))
        await using (var targetStream = new FileStream(filePath, FileMode.Create))
        {
            await sourceStream.CopyToAsync(targetStream);
        }

        var fileUrl = $"/uploads/attachments/{fileName}";
        return await _attachmentService.AddAttachmentAsync(
            messageId,
            messageType,
            AttachmentType.Image,
            fileUrl,
            file.Name,
            file.Size,
            file.ContentType,
            Path.GetFileNameWithoutExtension(file.Name)
        );
    }

    /// <summary>
    /// Добавить смайлик к сообщению
    /// </summary>
    public async Task<MessageAttachment> AddEmojiAsync(string messageId, string emojiCode, MessageAttachmentType messageType)
    {
        if (string.IsNullOrWhiteSpace(emojiCode))
            throw new ArgumentException("Код смайлика не может быть пустым");

        // Стандартные смайлики (можно расширить)
        var emojiMap = new Dictionary<string, string>
        {
            { "smile", "😊" },
            { "heart", "❤️" },
            { "fire", "🔥" },
            { "thumbsup", "👍" },
            { "thumbsdown", "👎" },
            { "laugh", "😂" },
            { "wow", "😮" },
            { "sad", "😢" },
            { "angry", "😠" },
            { "clap", "👏" },
            { "star", "⭐" },
            { "rocket", "🚀" },
            { "thinking", "🤔" }
        };

        if (!emojiMap.TryGetValue(emojiCode, out var emoji))
            throw new ArgumentException($"Неизвестный код смайлика: {emojiCode}");

        return await _attachmentService.AddAttachmentAsync(
            messageId,
            messageType,
            AttachmentType.Emoji,
            emoji,
            emojiCode,
            1,
            "text/plain",
            emojiCode
        );
    }

    public async Task<List<MessageAttachment>> GetAttachmentsAsync(string messageId)
    {
        return await _attachmentService.GetAttachmentsAsync(messageId);
    }

    public async Task<List<MessageAttachment>> GetAttachmentsByTypeAsync(string messageId, AttachmentType type)
    {
        return await _attachmentService.GetAttachmentsByTypeAsync(messageId, type);
    }

    public async Task DeleteAttachmentAsync(string attachmentId)
    {
        var attachment = await _attachmentService.GetAttachmentAsync(attachmentId);
        
        // Удалить файл если это локальный файл
        if (attachment.Type == AttachmentType.Image && attachment.FileUrl.StartsWith("/uploads/"))
        {
            var filePath = Path.Combine(_environment.WebRootPath, attachment.FileUrl.TrimStart('/'));
            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        await _attachmentService.DeleteAttachmentAsync(attachmentId);
    }

    public async Task<List<MessageAttachment>> GetRecentEmojisAsync(int count = 20)
    {
        return await _attachmentService.GetRecentEmojisAsync(count);
    }

    public Dictionary<string, string> GetEmojiDictionary()
    {
        return new Dictionary<string, string>
        {
            { "😊", "smile" },
            { "❤️", "heart" },
            { "🔥", "fire" },
            { "👍", "thumbsup" },
            { "👎", "thumbsdown" },
            { "😂", "laugh" },
            { "😮", "wow" },
            { "😢", "sad" },
            { "😠", "angry" },
            { "👏", "clap" },
            { "⭐", "star" },
            { "🚀", "rocket" },
            { "🤔", "thinking" }
        };
    }
}
