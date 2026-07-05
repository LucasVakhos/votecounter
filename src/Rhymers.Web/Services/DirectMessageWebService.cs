using Rhymers.Core.Data;
using Rhymers.Core.Models;
using Rhymers.Core.Services;

namespace Rhymers.Web.Services;

/// <summary>
/// Web-сервис управления приватными сообщениями (обертка для API и Blazor)
/// </summary>
public sealed class DirectMessageWebService
{
    private readonly DirectMessageService _directMessageService;
    private readonly RhymersDbContext _dbContext;

    public DirectMessageWebService(DirectMessageService directMessageService, RhymersDbContext dbContext)
    {
        _directMessageService = directMessageService;
        _dbContext = dbContext;
    }

    public async Task<UserDirectMessage> SendMessageAsync(string fromUserId, string toUserId, string content)
    {
        return await _directMessageService.SendMessageAsync(fromUserId, toUserId, content);
    }

    public async Task<List<(UserDirectMessage Message, User Sender, User Recipient)>> GetConversationWithDetailsAsync(
        string userId1, string userId2, int skip = 0, int take = 50)
    {
        var messages = await _directMessageService.GetConversationAsync(userId1, userId2, skip, take);
        var result = new List<(UserDirectMessage, User, User)>();

        var user1 = await _dbContext.Users.FindAsync(userId1);
        var user2 = await _dbContext.Users.FindAsync(userId2);

        if (user1 == null || user2 == null)
            return result;

        foreach (var message in messages)
        {
            var sender = message.FromUserId == userId1 ? user1 : user2;
            var recipient = message.ToUserId == userId1 ? user1 : user2;
            result.Add((message, sender, recipient));
        }

        return result;
    }

    public async Task<List<DialogInfo>> GetDialogsAsync(string userId)
    {
        var dialogs = await _directMessageService.GetDialogsAsync(userId);
        var result = new List<DialogInfo>();

        foreach (var (lastMessage, otherUserId) in dialogs)
        {
            var otherUser = await _dbContext.Users.FindAsync(otherUserId);
            if (otherUser != null)
            {
                var unreadCount = await _directMessageService.GetUnreadCountFromAsync(userId, otherUserId);
                result.Add(new DialogInfo
                {
                    OtherUser = otherUser,
                    LastMessage = lastMessage,
                    UnreadCount = unreadCount
                });
            }
        }

        return result;
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _directMessageService.GetUnreadCountAsync(userId);
    }

    public async Task<int> GetUnreadCountFromAsync(string userId, string fromUserId)
    {
        return await _directMessageService.GetUnreadCountFromAsync(userId, fromUserId);
    }

    public async Task MarkAsReadAsync(string messageId, string userId)
    {
        await _directMessageService.MarkAsReadAsync(messageId, userId);
    }

    public async Task MarkConversationAsReadAsync(string userId, string otherUserId)
    {
        await _directMessageService.MarkConversationAsReadAsync(userId, otherUserId);
    }

    public async Task<UserDirectMessage> EditMessageAsync(string messageId, string userId, string newContent)
    {
        return await _directMessageService.EditMessageAsync(messageId, userId, newContent);
    }

    public async Task DeleteMessageAsync(string messageId, string userId)
    {
        await _directMessageService.DeleteMessageAsync(messageId, userId);
    }

    public async Task ClearConversationAsync(string userId, string otherUserId)
    {
        await _directMessageService.ClearConversationAsync(userId, otherUserId);
    }

    public async Task<UserDirectMessage> GetMessageAsync(string messageId, string userId)
    {
        return await _directMessageService.GetMessageAsync(messageId, userId);
    }
}

/// <summary>
/// Информация о диалоге с другом
/// </summary>
public sealed class DialogInfo
{
    public User OtherUser { get; set; } = null!;
    public UserDirectMessage LastMessage { get; set; } = null!;
    public int UnreadCount { get; set; }
}
