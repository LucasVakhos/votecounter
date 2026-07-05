using Rhymers.Core.Data;
using Rhymers.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Rhymers.Core.Services;

/// <summary>
/// Сервис ведения журнала аудита действий администрации и модераторов
/// </summary>
public sealed class AuditLogService
{
    private readonly RhymersDbContext _context;

    public AuditLogService(RhymersDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Записать действие в журнал аудита
    /// </summary>
    public async Task LogAsync(AuditAction action, string actorName, UserRole actorRole,
        string targetUserName, string details,
        string? contestId = null, string? relatedEntityId = null)
    {
        var log = new AuditLog
        {
            Id = Guid.NewGuid().ToString("N"),
            Action = action,
            ActorName = actorName,
            ActorRole = actorRole,
            TargetUserName = targetUserName,
            ContestId = contestId,
            RelatedEntityId = relatedEntityId,
            Details = details,
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Получить журнал аудита с фильтрами
    /// </summary>
    public async Task<List<AuditLog>> GetLogsAsync(
        AuditAction? action = null,
        string? actorName = null,
        string? targetUserName = null,
        string? contestId = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 50)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (action.HasValue)
            query = query.Where(l => l.Action == action);

        if (!string.IsNullOrEmpty(actorName))
            query = query.Where(l => l.ActorName.Contains(actorName));

        if (!string.IsNullOrEmpty(targetUserName))
            query = query.Where(l => l.TargetUserName.Contains(targetUserName));

        if (!string.IsNullOrEmpty(contestId))
            query = query.Where(l => l.ContestId == contestId);

        if (from.HasValue)
            query = query.Where(l => l.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(l => l.CreatedAt <= to.Value);

        return await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    /// <summary>
    /// Получить количество записей (для пагинации)
    /// </summary>
    public async Task<int> GetLogsCountAsync(
        AuditAction? action = null,
        string? actorName = null,
        string? targetUserName = null,
        string? contestId = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (action.HasValue)
            query = query.Where(l => l.Action == action);

        if (!string.IsNullOrEmpty(actorName))
            query = query.Where(l => l.ActorName.Contains(actorName));

        if (!string.IsNullOrEmpty(targetUserName))
            query = query.Where(l => l.TargetUserName.Contains(targetUserName));

        if (!string.IsNullOrEmpty(contestId))
            query = query.Where(l => l.ContestId == contestId);

        if (from.HasValue)
            query = query.Where(l => l.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(l => l.CreatedAt <= to.Value);

        return await query.CountAsync();
    }
}
