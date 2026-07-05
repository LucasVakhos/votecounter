using Microsoft.EntityFrameworkCore;
using Rhymers.Core.Data;
using Rhymers.Core.Models;

namespace Rhymers.Core.Services;

/// <summary>
/// Сервис управления приватными сообщениями между пользователями
/// </summary>
public sealed class DirectMessageService
{
    private readonly RhymersDbContext _dbContext;
    private readonly FriendshipService _friendshipService;

    public DirectMessageService(RhymersDbContext dbContext, FriendshipService friendshipService)
    {
        _dbContext = dbContext;
        _friendshipService = friendshipService;
    }

    /// <summary>
    /// Отправить приватное сообщение
    /// </summary>
    public async Task<UserDirectMessage> SendMessageAsync(string fromUserId, string toUserId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Сообщение не может быть пустым");

        // Проверить, являются ли друзьями
        var areFriends = await _friendshipService.AreFriendsAsync(fromUserId, toUserId);
        if (!areFriends)
            throw new InvalidOperationException("Вы можете отправлять сообщения только друзьям");

        // Проверить блокировку
        var isBlocked = await _friendshipService.IsBlockedAsync(fromUserId, toUserId);
        if (isBlocked)
            throw new InvalidOperationException("Вы или получатель заблокировали переписку");

        var message = new UserDirectMessage
        {
            Id = Guid.NewGuid().ToString(),
            FromUserId = fromUserId,
            ToUserId = toUserId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.DirectMessages.Add(message);
        await _dbContext.SaveChangesAsync();
        return message;
    }

    /// <summary>
    /// Получить переписку между двумя пользователями (диалог)
    /// </summary>
    public async Task<List<UserDirectMessage>> GetConversationAsync(string userId1, string userId2, int skip = 0, int take = 50)
    {
        return await _dbContext.DirectMessages
            .Where(m => (m.FromUserId == userId1 && m.ToUserId == userId2 && !m.IsDeletedBySender) ||
                        (m.FromUserId == userId2 && m.ToUserId == userId1 && !m.IsDeletedByRecipient))
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Reverse()
            .ToListAsync();
    }

    /// <summary>
    /// Получить список диалогов пользователя (последний контакт с каждым другом)
    /// </summary>
    public async Task<List<(UserDirectMessage LastMessage, string OtherUserId)>> GetDialogsAsync(string userId)
    {
        var messages = await _dbContext.DirectMessages
            .Where(m => (m.FromUserId == userId || m.ToUserId == userId) &&
                        (!m.IsDeletedBySender && !m.IsDeletedByRecipient))
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        var dialogs = new List<(UserDirectMessage, string)>();
        var processedUsers = new HashSet<string>();

        foreach (var message in messages)
        {
            var otherUserId = message.FromUserId == userId ? message.ToUserId : message.FromUserId;
            
            if (!processedUsers.Contains(otherUserId))
            {
                dialogs.Add((message, otherUserId));
                processedUsers.Add(otherUserId);
            }
        }

        return dialogs;
    }

    /// <summary>
    /// Получить непрочитанные сообщения пользователя
    /// </summary>
    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _dbContext.DirectMessages
            .CountAsync(m => m.ToUserId == userId && !m.IsRead && !m.IsDeletedByRecipient);
    }

    /// <summary>
    /// Получить непрочитанные сообщения от конкретного пользователя
    /// </summary>
    public async Task<int> GetUnreadCountFromAsync(string userId, string fromUserId)
    {
        return await _dbContext.DirectMessages
            .CountAsync(m => m.ToUserId == userId && m.FromUserId == fromUserId && !m.IsRead && !m.IsDeletedByRecipient);
    }

    /// <summary>
    /// Отметить сообщение как прочитанное
    /// </summary>
    public async Task MarkAsReadAsync(string messageId, string userId)
    {
        var message = await _dbContext.DirectMessages.FindAsync(messageId)
            ?? throw new KeyNotFoundException("Сообщение не найдено");

        if (message.ToUserId != userId)
            throw new UnauthorizedAccessException("Это сообщение не адресовано вам");

        if (!message.IsRead)
        {
            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;
            _dbContext.DirectMessages.Update(message);
            await _dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Отметить все сообщения в диалоге как прочитанные
    /// </summary>
    public async Task MarkConversationAsReadAsync(string userId, string otherUserId)
    {
        var messages = await _dbContext.DirectMessages
            .Where(m => m.ToUserId == userId && m.FromUserId == otherUserId && !m.IsRead && !m.IsDeletedByRecipient)
            .ToListAsync();

        if (messages.Any())
        {
            var now = DateTime.UtcNow;
            foreach (var message in messages)
            {
                message.IsRead = true;
                message.ReadAt = now;
            }

            _dbContext.DirectMessages.UpdateRange(messages);
            await _dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Редактировать сообщение
    /// </summary>
    public async Task<UserDirectMessage> EditMessageAsync(string messageId, string userId, string newContent)
    {
        if (string.IsNullOrWhiteSpace(newContent))
            throw new ArgumentException("Сообщение не может быть пустым");

        var message = await _dbContext.DirectMessages.FindAsync(messageId)
            ?? throw new KeyNotFoundException("Сообщение не найдено");

        if (message.FromUserId != userId)
            throw new UnauthorizedAccessException("Вы можете редактировать только свои сообщения");

        // Можно редактировать сообщения не старше часа
        if (DateTime.UtcNow - message.CreatedAt > TimeSpan.FromHours(1))
            throw new InvalidOperationException("Сообщение слишком старое для редактирования");

        message.Content = newContent;
        message.EditedAt = DateTime.UtcNow;
        _dbContext.DirectMessages.Update(message);
        await _dbContext.SaveChangesAsync();
        return message;
    }

    /// <summary>
    /// Удалить сообщение для себя
    /// </summary>
    public async Task DeleteMessageAsync(string messageId, string userId)
    {
        var message = await _dbContext.DirectMessages.FindAsync(messageId)
            ?? throw new KeyNotFoundException("Сообщение не найдено");

        // Отправитель удаляет для себя
        if (message.FromUserId == userId)
        {
            message.IsDeletedBySender = true;
        }
        // Получатель удаляет для себя
        else if (message.ToUserId == userId)
        {
            message.IsDeletedByRecipient = true;
        }
        else
        {
            throw new UnauthorizedAccessException("Это сообщение вам не принадлежит");
        }

        _dbContext.DirectMessages.Update(message);
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Получить сообщение по ID с проверкой доступа
    /// </summary>
    public async Task<UserDirectMessage> GetMessageAsync(string messageId, string userId)
    {
        var message = await _dbContext.DirectMessages.FindAsync(messageId)
            ?? throw new KeyNotFoundException("Сообщение не найдено");

        if (message.FromUserId != userId && message.ToUserId != userId)
            throw new UnauthorizedAccessException("Доступ запрещен");

        return message;
    }

    /// <summary>
    /// Очистить диалог (удалить все сообщения в диалоге для себя)
    /// </summary>
    public async Task ClearConversationAsync(string userId, string otherUserId)
    {
        var messages = await _dbContext.DirectMessages
            .Where(m => (m.FromUserId == userId && m.ToUserId == otherUserId) ||
                        (m.FromUserId == otherUserId && m.ToUserId == userId))
            .ToListAsync();

        foreach (var message in messages)
        {
            if (message.FromUserId == userId)
                message.IsDeletedBySender = true;
            else
                message.IsDeletedByRecipient = true;
        }

        _dbContext.DirectMessages.UpdateRange(messages);
        await _dbContext.SaveChangesAsync();
    }
}
