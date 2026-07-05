using Rhymers.Core.Models;
using Rhymers.Core.Data;
using Rhymers.Data.Database;
using Microsoft.EntityFrameworkCore;

namespace Rhymers.Web.Services;

/// <summary>
/// Сервис для работы с конкурсами в Blazor приложении
/// </summary>
public class ContestService
{
    private readonly RhymersDbContext _context;
    private List<VoteEntry> _votes = new();

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
        var contest = await _context.Contests.FirstOrDefaultAsync(c => c.Id == id);
        return contest;
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
}
