using Microsoft.EntityFrameworkCore;
using Rhymers.Core.Data;
using Rhymers.Core.Models;

namespace Rhymers.Core.Services;

/// <summary>
/// Сервис управления вложениями (картинки, смайлики) в сообщениях
/// </summary>
public sealed class AttachmentService
{
    private readonly RhymersDbContext _dbContext;

    public AttachmentService(RhymersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Добавить вложение к сообщению
    /// </summary>
    public async Task<MessageAttachment> AddAttachmentAsync(
        string messageId,
        MessageAttachmentType messageType,
        AttachmentType attachmentType,
        string fileUrl,
        string? fileName = null,
        long fileSize = 0,
        string? mimeType = null,
        string? altText = null)
    {
        var attachment = new MessageAttachment
        {
            Id = Guid.NewGuid().ToString(),
            MessageId = messageId,
            MessageType = messageType,
            Type = attachmentType,
            FileUrl = fileUrl,
            FileName = fileName,
            FileSize = fileSize,
            MimeType = mimeType,
            AltText = altText
        };

        _dbContext.MessageAttachments.Add(attachment);
        await _dbContext.SaveChangesAsync();
        return attachment;
    }

    /// <summary>
    /// Получить все вложения сообщения
    /// </summary>
    public async Task<List<MessageAttachment>> GetAttachmentsAsync(string messageId)
    {
        return await _dbContext.MessageAttachments
            .Where(a => a.MessageId == messageId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Получить вложения по типу
    /// </summary>
    public async Task<List<MessageAttachment>> GetAttachmentsByTypeAsync(string messageId, AttachmentType type)
    {
        return await _dbContext.MessageAttachments
            .Where(a => a.MessageId == messageId && a.Type == type)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Удалить вложение
    /// </summary>
    public async Task DeleteAttachmentAsync(string attachmentId)
    {
        var attachment = await _dbContext.MessageAttachments.FindAsync(attachmentId)
            ?? throw new KeyNotFoundException("Вложение не найдено");

        _dbContext.MessageAttachments.Remove(attachment);
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Получить вложение по ID
    /// </summary>
    public async Task<MessageAttachment> GetAttachmentAsync(string attachmentId)
    {
        return await _dbContext.MessageAttachments.FindAsync(attachmentId)
            ?? throw new KeyNotFoundException("Вложение не найдено");
    }

    /// <summary>
    /// Получить популярные смайлики (используемые в последнее время)
    /// </summary>
    public async Task<List<MessageAttachment>> GetRecentEmojisAsync(int count = 20)
    {
        return await _dbContext.MessageAttachments
            .Where(a => a.Type == AttachmentType.Emoji)
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>
    /// Получить статистику использования вложений
    /// </summary>
    public async Task<(int TotalAttachments, int Images, int Emojis, long TotalSize)> GetStatisticsAsync()
    {
        var attachments = await _dbContext.MessageAttachments.ToListAsync();

        return (
            attachments.Count,
            attachments.Count(a => a.Type == AttachmentType.Image),
            attachments.Count(a => a.Type == AttachmentType.Emoji),
            attachments.Sum(a => a.FileSize)
        );
    }
}
