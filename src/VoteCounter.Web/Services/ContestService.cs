using VoteCounter.Core.Models;
using VoteCounter.Data.Database;

namespace VoteCounter.Web.Services;

/// <summary>
/// Сервис для работы с конкурсами в Blazor приложении
/// </summary>
public class ContestService
{
    private List<Contest> _contests = new();
    private List<VoteEntry> _votes = new();

    /// <summary>
    /// Получить все конкурсы
    /// </summary>
    public async Task<IEnumerable<Contest>> GetContestsAsync()
    {
        // TODO: Загрузить из базы данных
        return await Task.FromResult(_contests);
    }

    /// <summary>
    /// Получить конкурс по ID
    /// </summary>
    public async Task<Contest?> GetContestAsync(string id)
    {
        var contest = _contests.FirstOrDefault(c => c.Id == id);
        return await Task.FromResult(contest);
    }

    /// <summary>
    /// Создать новый конкурс
    /// </summary>
    public async Task<Contest> CreateContestAsync(string name)
    {
        var contest = new Contest
        {
            Id = Guid.NewGuid().ToString("N"),
            Number = (_contests.Count + 1).ToString("000"),
            Name = name,
            HostName = "Unknown",
            StartedAt = DateTime.Now
        };

        _contests.Add(contest);
        
        // TODO: Сохранить в базу данных
        return await Task.FromResult(contest);
    }

    /// <summary>
    /// Добавить работу в конкурс
    /// </summary>
    public async Task AddWorkAsync(string contestId, ContestWork work)
    {
        var contest = _contests.FirstOrDefault(c => c.Id == contestId);
        if (contest != null)
        {
            contest.Works.Add(work);
            // TODO: Сохранить в базу данных
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Получить работы конкурса
    /// </summary>
    public async Task<IEnumerable<ContestWork>> GetWorksAsync(string contestId)
    {
        var contest = _contests.FirstOrDefault(c => c.Id == contestId);
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
