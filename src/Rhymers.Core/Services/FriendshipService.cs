using Microsoft.EntityFrameworkCore;
using Rhymers.Core.Data;
using Rhymers.Core.Models;

namespace Rhymers.Core.Services;

/// <summary>
/// Сервис управления дружбой между пользователями
/// </summary>
public sealed class FriendshipService
{
    private readonly RhymersDbContext _dbContext;

    public FriendshipService(RhymersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Отправить приглашение в друзья
    /// </summary>
    public async Task<FriendshipInvitation> SendInvitationAsync(string fromUserId, string toUserId, string? message = null)
    {
        // Проверить, не дружат ли уже
        var existingFriendship = await _dbContext.UserFriendships
            .Where(f => (f.UserId1 == fromUserId && f.UserId2 == toUserId) ||
                        (f.UserId1 == toUserId && f.UserId2 == fromUserId))
            .FirstOrDefaultAsync();

        if (existingFriendship != null)
            throw new InvalidOperationException("Вы уже друзья или дружба заблокирована");

        // Проверить, нет ли уже висящего приглашения
        var existingInvitation = await _dbContext.FriendshipInvitations
            .Where(i => i.FromUserId == fromUserId && i.ToUserId == toUserId && i.Status == InvitationStatus.Pending)
            .FirstOrDefaultAsync();

        if (existingInvitation != null)
            throw new InvalidOperationException("Приглашение уже отправлено");

        var invitation = new FriendshipInvitation
        {
            Id = Guid.NewGuid().ToString(),
            FromUserId = fromUserId,
            ToUserId = toUserId,
            Message = message,
            Status = InvitationStatus.Pending
        };

        _dbContext.FriendshipInvitations.Add(invitation);
        await _dbContext.SaveChangesAsync();
        return invitation;
    }

    /// <summary>
    /// Принять приглашение в друзья
    /// </summary>
    public async Task<UserFriendship> AcceptInvitationAsync(string invitationId, string userId)
    {
        var invitation = await _dbContext.FriendshipInvitations.FindAsync(invitationId)
            ?? throw new KeyNotFoundException("Приглашение не найдено");

        if (invitation.ToUserId != userId)
            throw new UnauthorizedAccessException("Это приглашение не адресовано вам");

        if (invitation.Status != InvitationStatus.Pending)
            throw new InvalidOperationException("Это приглашение уже обработано");

        // Создать дружбу
        var friendship = new UserFriendship
        {
            Id = Guid.NewGuid().ToString(),
            UserId1 = invitation.FromUserId,
            UserId2 = invitation.ToUserId,
            Status = FriendshipStatus.Active
        };

        invitation.Status = InvitationStatus.Accepted;
        invitation.ProcessedAt = DateTime.UtcNow;
        invitation.ProcessedBy = userId;

        _dbContext.FriendshipInvitations.Update(invitation);
        _dbContext.UserFriendships.Add(friendship);
        await _dbContext.SaveChangesAsync();
        return friendship;
    }

    /// <summary>
    /// Отклонить приглашение в друзья
    /// </summary>
    public async Task DeclineInvitationAsync(string invitationId, string userId)
    {
        var invitation = await _dbContext.FriendshipInvitations.FindAsync(invitationId)
            ?? throw new KeyNotFoundException("Приглашение не найдено");

        if (invitation.ToUserId != userId)
            throw new UnauthorizedAccessException("Это приглашение не адресовано вам");

        if (invitation.Status != InvitationStatus.Pending)
            throw new InvalidOperationException("Это приглашение уже обработано");

        invitation.Status = InvitationStatus.Declined;
        invitation.ProcessedAt = DateTime.UtcNow;
        invitation.ProcessedBy = userId;

        _dbContext.FriendshipInvitations.Update(invitation);
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Получить список друзей пользователя
    /// </summary>
    public async Task<List<UserFriendship>> GetFriendsAsync(string userId)
    {
        return await _dbContext.UserFriendships
            .Where(f => (f.UserId1 == userId || f.UserId2 == userId) && f.Status == FriendshipStatus.Active)
            .ToListAsync();
    }

    /// <summary>
    /// Получить входящие приглашения в друзья
    /// </summary>
    public async Task<List<FriendshipInvitation>> GetIncomingInvitationsAsync(string userId)
    {
        return await _dbContext.FriendshipInvitations
            .Where(i => i.ToUserId == userId && i.Status == InvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Получить исходящие приглашения в друзья
    /// </summary>
    public async Task<List<FriendshipInvitation>> GetOutgoingInvitationsAsync(string userId)
    {
        return await _dbContext.FriendshipInvitations
            .Where(i => i.FromUserId == userId && i.Status == InvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Удалить из друзей
    /// </summary>
    public async Task RemoveFriendAsync(string userId, string friendId)
    {
        var friendship = await _dbContext.UserFriendships
            .Where(f => (f.UserId1 == userId && f.UserId2 == friendId) ||
                        (f.UserId1 == friendId && f.UserId2 == userId))
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Дружба не найдена");

        friendship.Status = FriendshipStatus.Deleted;
        friendship.UpdatedAt = DateTime.UtcNow;
        _dbContext.UserFriendships.Update(friendship);
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Заблокировать пользователя
    /// </summary>
    public async Task BlockUserAsync(string blockingUserId, string blockedUserId)
    {
        var friendship = await _dbContext.UserFriendships
            .Where(f => (f.UserId1 == blockingUserId && f.UserId2 == blockedUserId) ||
                        (f.UserId1 == blockedUserId && f.UserId2 == blockingUserId))
            .FirstOrDefaultAsync();

        if (friendship == null)
        {
            // Если дружбы нет, создать блокировку
            friendship = new UserFriendship
            {
                Id = Guid.NewGuid().ToString(),
                UserId1 = blockingUserId,
                UserId2 = blockedUserId
            };
            _dbContext.UserFriendships.Add(friendship);
        }

        // Определить, кто инициатор блокировки
        if (friendship.UserId1 == blockingUserId)
            friendship.Status = FriendshipStatus.BlockedByUser1;
        else
            friendship.Status = FriendshipStatus.BlockedByUser2;

        friendship.UpdatedAt = DateTime.UtcNow;
        _dbContext.UserFriendships.Update(friendship);
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Проверить, являются ли пользователи друзьями
    /// </summary>
    public async Task<bool> AreFriendsAsync(string userId1, string userId2)
    {
        return await _dbContext.UserFriendships
            .AnyAsync(f => (f.UserId1 == userId1 && f.UserId2 == userId2 ||
                            f.UserId1 == userId2 && f.UserId2 == userId1) &&
                           f.Status == FriendshipStatus.Active);
    }

    /// <summary>
    /// Проверить, заблокирован ли пользователь
    /// </summary>
    public async Task<bool> IsBlockedAsync(string userId1, string userId2)
    {
        return await _dbContext.UserFriendships
            .AnyAsync(f => (f.UserId1 == userId1 && f.UserId2 == userId2 ||
                            f.UserId1 == userId2 && f.UserId2 == userId1) &&
                           (f.Status == FriendshipStatus.BlockedByUser1 || f.Status == FriendshipStatus.BlockedByUser2));
    }
}
