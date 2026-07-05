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
        await ApplyAutomaticStageSwitchesAsync();
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

    public async Task<Contest?> GetCurrentContestAsync()
    {
        await ApplyAutomaticStageSwitchesAsync();

        return await _context.Contests
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.StageUpdatedAt)
            .ThenByDescending(c => c.UpdatedAt)
            .ThenByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync()
            ?? await _context.Contests
                .OrderByDescending(c => c.StageUpdatedAt)
                .ThenByDescending(c => c.UpdatedAt)
                .ThenByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();
    }

    public async Task<List<WorkSubmission>> GetApprovedWorkSubmissionsAsync(string contestId)
    {
        return await _context.Submissions
            .Where(s => s.ContestId == contestId && s.Status == WorkStatus.Approved)
            .OrderBy(s => s.SubmittedAt)
            .ThenBy(s => s.Id)
            .ToListAsync();
    }

    public async Task<Dictionary<string, ContestVote>> GetUserContestVotesAsync(string contestId, string voterUserId)
    {
        var votes = await _context.ContestVotes
            .Where(v => v.ContestId == contestId && v.VoterUserId == voterUserId)
            .ToListAsync();

        return votes.ToDictionary(v => v.SubmissionId, v => v);
    }

    public async Task SaveUserContestVotesAsync(
        string contestId,
        string voterUserId,
        string voterUsername,
        IEnumerable<(string SubmissionId, int Score, string Comment)> votes)
    {
        foreach (var item in votes)
        {
            var existing = await _context.ContestVotes.FirstOrDefaultAsync(v =>
                v.ContestId == contestId &&
                v.SubmissionId == item.SubmissionId &&
                v.VoterUserId == voterUserId);

            if (existing == null)
            {
                existing = new ContestVote
                {
                    ContestId = contestId,
                    SubmissionId = item.SubmissionId,
                    VoterUserId = voterUserId,
                    VoterUsername = voterUsername,
                    Score = item.Score,
                    Comment = item.Comment,
                    UpdatedAt = DateTime.Now
                };
                _context.ContestVotes.Add(existing);
            }
            else
            {
                existing.Score = item.Score;
                existing.Comment = item.Comment;
                existing.UpdatedAt = DateTime.Now;
                _context.ContestVotes.Update(existing);
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<ContestVote>> GetContestVotesAsync(string contestId)
    {
        return await _context.ContestVotes
            .Where(v => v.ContestId == contestId)
            .OrderBy(v => v.SubmissionId)
            .ThenBy(v => v.VoterUserId)
            .ToListAsync();
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

    public async Task<bool> SetContestStageAsync(string contestId, ContestStage stage)
    {
        var contest = await _context.Contests.FirstOrDefaultAsync(c => c.Id == contestId);
        if (contest == null)
            return false;

        contest.Stage = (int)stage;
        contest.StageUpdatedAt = DateTime.Now;
        contest.UpdatedAt = DateTime.Now;

        if (stage == ContestStage.Finished)
        {
            contest.IsActive = false;
            contest.ClosedAt ??= DateTime.Now;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SaveStageAutomationSettingsAsync(
        string contestId,
        bool autoStageSwitchEnabled,
        int? topicReceptionSwitchDayOfWeek,
        string topicReceptionSwitchTime,
        int? workReceptionSwitchDayOfWeek,
        string workReceptionSwitchTime,
        int? votingOpenSwitchDayOfWeek,
        string votingOpenSwitchTime,
        int? votingClosedSwitchDayOfWeek,
        string votingClosedSwitchTime)
    {
        var contest = await _context.Contests.FirstOrDefaultAsync(c => c.Id == contestId);
        if (contest == null)
            return false;

        contest.AutoStageSwitchEnabled = autoStageSwitchEnabled;
        contest.TopicReceptionSwitchDayOfWeek = NormalizeDayOfWeek(topicReceptionSwitchDayOfWeek);
        contest.TopicReceptionSwitchTime = NormalizeSwitchTime(topicReceptionSwitchTime);
        contest.WorkReceptionSwitchDayOfWeek = NormalizeDayOfWeek(workReceptionSwitchDayOfWeek);
        contest.WorkReceptionSwitchTime = NormalizeSwitchTime(workReceptionSwitchTime);
        contest.VotingOpenSwitchDayOfWeek = NormalizeDayOfWeek(votingOpenSwitchDayOfWeek);
        contest.VotingOpenSwitchTime = NormalizeSwitchTime(votingOpenSwitchTime);
        contest.VotingClosedSwitchDayOfWeek = NormalizeDayOfWeek(votingClosedSwitchDayOfWeek);
        contest.VotingClosedSwitchTime = NormalizeSwitchTime(votingClosedSwitchTime);
        contest.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> ApplyAutomaticStageSwitchesAsync()
    {
        var now = DateTime.Now;
        var contests = await _context.Contests
            .Where(c => c.AutoStageSwitchEnabled)
            .Where(c => c.Stage != (int)ContestStage.Finished)
            .ToListAsync();

        var changed = 0;
        foreach (var contest in contests)
        {
            var next = GetAutoStageForMoment(contest, now);
            if (next == GetContestStage(contest))
                continue;

            contest.Stage = (int)next;
            contest.StageUpdatedAt = now;
            contest.UpdatedAt = now;
            if (next == ContestStage.Finished)
            {
                contest.IsActive = false;
                contest.ClosedAt ??= now;
            }

            changed++;
        }

        if (changed > 0)
            await _context.SaveChangesAsync();

        return changed;
    }

    public static ContestStage GetContestStage(Contest contest)
    {
        var stage = contest.Stage;
        return Enum.IsDefined(typeof(ContestStage), stage)
            ? (ContestStage)stage
            : ContestStage.TopicReception;
    }

    public static string GetContestStageTitle(ContestStage stage)
    {
        return stage switch
        {
            ContestStage.TopicReception => "Прием тем",
            ContestStage.WorkReception => "Прием работ",
            ContestStage.VotingOpen => "Голосование открыто",
            ContestStage.VotingClosed => "Голосование закрыто",
            ContestStage.Finished => "Конкурс завершен",
            _ => "Неизвестно"
        };
    }

    public async Task<List<TopicKind>> GetTopicKindsAsync()
    {
        return await _context.TopicKinds
            .OrderBy(k => k.SortNo)
            .ThenBy(k => k.Name)
            .ToListAsync();
    }

    public async Task<TopicKind?> AddTopicKindAsync(string name)
    {
        var normalized = NormalizeKindName(name);
        if (normalized.Length == 0)
            return null;

        var exists = await _context.TopicKinds.AnyAsync(k => k.Name.ToLower() == normalized.ToLower());
        if (exists)
            return null;

        var nextSort = await _context.TopicKinds.Select(k => (int?)k.SortNo).MaxAsync() ?? 0;
        var kind = new TopicKind
        {
            Name = normalized,
            SortNo = nextSort + 1
        };

        _context.TopicKinds.Add(kind);
        await _context.SaveChangesAsync();
        return kind;
    }

    public async Task<bool> RenameTopicKindAsync(int kindId, string newName)
    {
        var normalized = NormalizeKindName(newName);
        if (normalized.Length == 0)
            return false;

        var kind = await _context.TopicKinds.FirstOrDefaultAsync(k => k.Id == kindId);
        if (kind == null)
            return false;

        var duplicate = await _context.TopicKinds
            .AnyAsync(k => k.Id != kindId && k.Name.ToLower() == normalized.ToLower());
        if (duplicate)
            return false;

        kind.Name = normalized;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<(bool success, string error)> DeleteTopicKindAsync(int kindId)
    {
        var kind = await _context.TopicKinds.FirstOrDefaultAsync(k => k.Id == kindId);
        if (kind == null)
            return (false, "Разновидность не найдена.");

        var hasTopics = await _context.Topics.AnyAsync(t => t.TopicKindId == kindId);
        if (hasTopics)
            return (false, "Нельзя удалить разновидность, которая уже используется в темах.");

        _context.TopicKinds.Remove(kind);
        await _context.SaveChangesAsync();
        return (true, string.Empty);
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
        string proposedBy,
        int? winnerTopicKindId,
        int? regularTopicKindId)
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
                TopicKindId = winnerTopicKindId,
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
                TopicKindId = regularTopicKindId,
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

    private static ContestStage GetAutoStageForMoment(Contest contest, DateTime now)
    {
        var stage = GetContestStage(contest);
        var stageAnchor = contest.StageUpdatedAt == default ? contest.StartedAt : contest.StageUpdatedAt;

        while (true)
        {
            var next = stage switch
            {
                ContestStage.TopicReception when
                    TryGetScheduledSwitchMoment(stageAnchor, contest.TopicReceptionSwitchDayOfWeek, contest.TopicReceptionSwitchTime, out var topicSwitch) &&
                    now >= topicSwitch
                    => ContestStage.WorkReception,
                ContestStage.WorkReception when
                    TryGetScheduledSwitchMoment(stageAnchor, contest.WorkReceptionSwitchDayOfWeek, contest.WorkReceptionSwitchTime, out var workSwitch) &&
                    now >= workSwitch
                    => ContestStage.VotingOpen,
                ContestStage.VotingOpen when
                    TryGetScheduledSwitchMoment(stageAnchor, contest.VotingOpenSwitchDayOfWeek, contest.VotingOpenSwitchTime, out var votingOpenSwitch) &&
                    now >= votingOpenSwitch
                    => ContestStage.VotingClosed,
                ContestStage.VotingClosed when
                    TryGetScheduledSwitchMoment(stageAnchor, contest.VotingClosedSwitchDayOfWeek, contest.VotingClosedSwitchTime, out var votingClosedSwitch) &&
                    now >= votingClosedSwitch
                    => ContestStage.Finished,
                _ => stage
            };

            if (next == stage)
                return stage;

            stage = next;
            stageAnchor = now;
        }
    }

    private static int? NormalizeDayOfWeek(int? dayOfWeek)
    {
        if (!dayOfWeek.HasValue)
            return null;

        return dayOfWeek.Value is >= 0 and <= 6 ? dayOfWeek : null;
    }

    private static string NormalizeSwitchTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return TimeSpan.TryParse(value, out var parsed)
            ? $"{parsed.Hours:D2}:{parsed.Minutes:D2}"
            : string.Empty;
    }

    private static bool TryGetScheduledSwitchMoment(DateTime anchor, int? dayOfWeek, string? timeText, out DateTime scheduled)
    {
        scheduled = default;
        if (!dayOfWeek.HasValue || string.IsNullOrWhiteSpace(timeText))
            return false;

        if (!TimeSpan.TryParse(timeText, out var time))
            return false;

        var targetDay = dayOfWeek.Value;
        if (targetDay is < 0 or > 6)
            return false;

        var anchorDate = anchor.Date;
        var delta = (targetDay - (int)anchorDate.DayOfWeek + 7) % 7;
        var scheduledDate = anchorDate.AddDays(delta);
        var candidate = scheduledDate.Add(time);
        if (candidate < anchor)
            candidate = candidate.AddDays(7);

        scheduled = candidate;
        return true;
    }

    private static string NormalizeKindName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return string.Join(' ', value
            .Trim()
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
    }
}
