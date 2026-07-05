using Microsoft.EntityFrameworkCore;
using Rhymers.Core.Data;
using Rhymers.Core.Models;
using Rhymers.Core.Services;

namespace Rhymers.Web.Services;

/// <summary>
/// Web-сервис управления дружбой (обертка для API и Blazor)
/// </summary>
public sealed class FriendshipWebService
{
    private readonly FriendshipService _friendshipService;
    private readonly RhymersDbContext _dbContext;

    public FriendshipWebService(FriendshipService friendshipService, RhymersDbContext dbContext)
    {
        _friendshipService = friendshipService;
        _dbContext = dbContext;
    }

    public async Task<FriendshipInvitation> SendInvitationAsync(string fromUserId, string toUserId, string? message = null)
    {
        return await _friendshipService.SendInvitationAsync(fromUserId, toUserId, message);
    }

    public async Task<UserFriendship> AcceptInvitationAsync(string invitationId, string userId)
    {
        return await _friendshipService.AcceptInvitationAsync(invitationId, userId);
    }

    public async Task DeclineInvitationAsync(string invitationId, string userId)
    {
        await _friendshipService.DeclineInvitationAsync(invitationId, userId);
    }

    public async Task<List<(User Friend, UserFriendship Friendship)>> GetFriendsWithDetailsAsync(string userId)
    {
        var friendships = await _friendshipService.GetFriendsAsync(userId);
        var result = new List<(User, UserFriendship)>();

        foreach (var friendship in friendships)
        {
            var friendId = friendship.GetOtherId(userId);
            var friend = await _dbContext.Users.FindAsync(friendId);
            if (friend != null)
                result.Add((friend, friendship));
        }

        return result;
    }

    public async Task<List<(User Sender, FriendshipInvitation Invitation)>> GetIncomingInvitationsWithDetailsAsync(string userId)
    {
        var invitations = await _friendshipService.GetIncomingInvitationsAsync(userId);
        var result = new List<(User, FriendshipInvitation)>();

        foreach (var invitation in invitations)
        {
            var sender = await _dbContext.Users.FindAsync(invitation.FromUserId);
            if (sender != null)
                result.Add((sender, invitation));
        }

        return result;
    }

    public async Task<List<(User Recipient, FriendshipInvitation Invitation)>> GetOutgoingInvitationsWithDetailsAsync(string userId)
    {
        var invitations = await _friendshipService.GetOutgoingInvitationsAsync(userId);
        var result = new List<(User, FriendshipInvitation)>();

        foreach (var invitation in invitations)
        {
            var recipient = await _dbContext.Users.FindAsync(invitation.ToUserId);
            if (recipient != null)
                result.Add((recipient, invitation));
        }

        return result;
    }

    public async Task RemoveFriendAsync(string userId, string friendId)
    {
        await _friendshipService.RemoveFriendAsync(userId, friendId);
    }

    public async Task BlockUserAsync(string blockingUserId, string blockedUserId)
    {
        await _friendshipService.BlockUserAsync(blockingUserId, blockedUserId);
    }

    public async Task<bool> AreFriendsAsync(string userId1, string userId2)
    {
        return await _friendshipService.AreFriendsAsync(userId1, userId2);
    }

    public async Task<bool> IsBlockedAsync(string userId1, string userId2)
    {
        return await _friendshipService.IsBlockedAsync(userId1, userId2);
    }

    public async Task<User?> FindUserAsync(string username)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == username || u.DisplayName == username);
    }
}
