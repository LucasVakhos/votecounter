using Rhymers.Core.Models;
using Rhymers.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Rhymers.Web.Services;

/// <summary>
/// Сервис для работы с конкурсами в Blazor приложении
/// </summary>
public class ContestService
{
    private readonly RhymersDbContext _context;
    private readonly List<VoteEntry> _votes = new();

    public ContestService(RhymersDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Получить все конкурсы
    /// </summary>
    public async Task<IEnumerable<Contest>> GetContestsAsync()
    {
        var contests = await _context.Contests.OrderByDescending(c => c.CreatedAt).ToListAsync();
        return contests;
    }

    /// <summary>
    /// Получить конкурс по ID
    /// </summary>
    public async Task<Contest?> GetContestAsync(string id)
    {
        return await _context.Contests.FirstOrDefaultAsync(c => c.Id == id);
    }

    /// <summary>
    /// Создать новый конкурс
    /// </summary>
    public async Task<Contest> CreateContestAsync(string name)
    {
        var contests = await _context.Contests.ToListAsync();
        var contest = new Contest
        {
            Id = Guid.NewGuid().ToString("N"),
            Number = (contests.Count + 1).ToString("000"),
            Name = name,
            HostName = "Unknown",
            StartedAt = DateTime.Now
        };

        _context.Contests.Add(contest);
        await _context.SaveChangesAsync();
        
        return contest;
    }

    public async Task<List<ContestTopic>> GetContestTopicsAsync(string contestId)
    {
        return await _context.Topics
            .Where(t => t.ContestId == contestId)
            .OrderByDescending(t => t.IsWinnerTopic)
            .ThenBy(t => t.Number)
            .ToListAsync();
    }

    public async Task SaveMaxTopicsCountAsync(string contestId, int maxTopicsCount)
    {
        var contest = await _context.Contests.FirstOrDefaultAsync(c => c.Id == contestId);
        if (contest == null)
            return;

        contest.MaxTopicsCount = Math.Max(0, maxTopicsCount);
        contest.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    public async Task<List<string>> GetPreviousContestWinnerTopicSuggestionsAsync(string contestId, int take = 8)
    {
        var contests = await _context.Contests
            .OrderBy(c => c.StartedAt)
            .ThenBy(c => c.CreatedAt)
            .ToListAsync();

        var current = contests.FirstOrDefault(c => c.Id == contestId);
        if (current == null)
            return new List<string>();

        var previous = contests
            .Where(c => !string.Equals(c.Id, contestId, StringComparison.OrdinalIgnoreCase))
            .Where(c => c.StartedAt <= current.StartedAt || ParseContestNumber(c.Number) < ParseContestNumber(current.Number))
            .OrderByDescending(c => ParseContestNumber(c.Number))
            .ThenByDescending(c => c.StartedAt)
            .FirstOrDefault();

        if (previous == null)
            return new List<string>();

        var winnerTopics = await _context.Topics
            .Where(t => t.ContestId == previous.Id && t.IsWinnerTopic)
            .OrderBy(t => t.Number)
            .Select(t => t.Title)
            .ToListAsync();

        if (winnerTopics.Count > 0)
        {
            return winnerTopics
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(take)
                .ToList();
        }

        return await _context.Topics
            .Where(t => t.ContestId == previous.Id)
            .OrderBy(t => t.Number)
            .Select(t => t.Title)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .Take(take)
            .ToListAsync();
    }

    public async Task<(int addedWinner, int addedRegular, int skipped)> AddTopicsWithPriorityAsync(
        string contestId,
        IEnumerable<string> winnerTopics,
        IEnumerable<string> regularTopics,
        string proposedBy)
    {
        var contest = await _context.Contests.FirstOrDefaultAsync(c => c.Id == contestId);
        if (contest == null)
            return (0, 0, 0);

        var existing = await _context.Topics
            .Where(t => t.ContestId == contestId)
            .OrderBy(t => t.Number)
            .ToListAsync();

        var existingTitles = existing
            .Select(t => NormalizeTopicTitle(t.Title))
            .Where(t => t.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var maxCount = contest.MaxTopicsCount > 0 ? contest.MaxTopicsCount : int.MaxValue;
        var nextNumber = existing.Count == 0 ? 1 : existing.Max(t => t.Number) + 1;

        var addedWinner = 0;
        var addedRegular = 0;
        var skipped = 0;

        foreach (var title in NormalizeIncomingTopics(winnerTopics))
        {
            if (existing.Count >= maxCount)
            {
                skipped++;
                continue;
            }

            var key = NormalizeTopicTitle(title);
            if (!existingTitles.Add(key))
            {
                skipped++;
                continue;
            }

            var topic = new ContestTopic
            {
                ContestId = contestId,
                Number = nextNumber++,
                Title = title,
                ProposedBy = proposedBy,
                IsWinnerTopic = true,
                SubmittedAt = DateTime.Now
            };

            existing.Add(topic);
            _context.Topics.Add(topic);
            addedWinner++;
        }

        foreach (var title in NormalizeIncomingTopics(regularTopics))
        {
            if (existing.Count >= maxCount)
            {
                skipped++;
                continue;
            }

            var key = NormalizeTopicTitle(title);
            if (!existingTitles.Add(key))
            {
                skipped++;
                continue;
            }

            var topic = new ContestTopic
            {
                ContestId = contestId,
                Number = nextNumber++,
                Title = title,
                ProposedBy = proposedBy,
                IsWinnerTopic = false,
                SubmittedAt = DateTime.Now
            };

            existing.Add(topic);
            _context.Topics.Add(topic);
            addedRegular++;
        }

        contest.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        return (addedWinner, addedRegular, skipped);
    }

    /// <summary>
    /// Добавить работу в конкурс
    /// </summary>
    public async Task AddWorkAsync(string contestId, ContestWork work)
    {
        var contest = await _context.Contests.FirstOrDefaultAsync(c => c.Id == contestId);
        if (contest != null)
        {
            contest.Works.Add(work);
            _context.Contests.Update(contest);
            await _context.SaveChangesAsync();
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Получить работы конкурса
    /// </summary>
    public async Task<IEnumerable<ContestWork>> GetWorksAsync(string contestId)
    {
        var contest = await _context.Contests.FirstOrDefaultAsync(c => c.Id == contestId);
        return await Task.FromResult(contest?.Works ?? new List<ContestWork>());
    }

    /// <summary>
    /// Сохранить голоса
    /// </summary>
    public async Task SaveVotesAsync(string contestId, List<VoteEntry> votes)
    {
        // Привязать голоса к конкурсу
        foreach (var vote in votes)
        {
            vote.ContestId = contestId;
        }
        _votes.AddRange(votes);
        // TODO: Сохранить в базу данных
        await Task.CompletedTask;
    }

    /// <summary>
    /// Получить голоса конкурса
    /// </summary>
    public async Task<List<VoteEntry>> GetVotesAsync(string contestId)
    {
        var votes = _votes.Where(v => v.ContestId == contestId).ToList();
        return await Task.FromResult(votes);
    }

    private static IEnumerable<string> NormalizeIncomingTopics(IEnumerable<string> source)
    {
        return source
            .Select(s => s?.Trim() ?? string.Empty)
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeTopicTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var normalized = string.Join(' ', title
            .Trim()
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));

        return normalized.ToLowerInvariant();
    }

    private static int ParseContestNumber(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
            return 0;

        return int.TryParse(number, out var parsed) ? parsed : 0;
    }
}
