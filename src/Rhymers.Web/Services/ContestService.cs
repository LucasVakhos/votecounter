using Rhymers.Core.Models;
using Rhymers.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Rhymers.Web.Services;

/// <summary>
/// Сервис для работы с конкурсами в Blazor приложении
/// </summary>
public class ContestService
{
    private const string FairVotingSystemUserId = "system:fair-voting";
    private const string FairVotingSystemUsername = "FairVotingBot";
    private const string FairVotingComment = "Честная автооценка системы";
    private const string AdminAverageSystemUserId = "system:admin-average";
    private const string AdminAverageSystemUsername = "AdminAverageBot";
    private const string AdminAverageComment = "Автоголосование администрации по среднему баллу";
    private const string DefaultSanctionWarningMessage = "Пользователь {username}, выявлены признаки недобросовестной конкуренции ({reason}). Если нарушение повторится, будут применены санкции: аннулирование голосов, дисквалификация работы и ограничение участия.";

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
            .OrderBy(t => t.SubmittedAt)
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

    public async Task<bool> SaveWinnersPraiseTextAsync(string contestId, string text)
    {
        var contest = await _context.Contests.FirstOrDefaultAsync(c => c.Id == contestId);
        if (contest == null)
            return false;

        contest.WinnersPraiseText = (text ?? string.Empty).Trim();
        contest.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> SendSanctionsWarningToUsersAsync(string createdBy, string customMessage, IEnumerable<string> userIds)
    {
        return await SendSanctionsWarningToUsersPersonalizedAsync(createdBy, customMessage, userIds, null, null);
    }

    public async Task<int> SendSanctionsWarningToUsersPersonalizedAsync(
        string createdBy,
        string template,
        IEnumerable<string> userIds,
        IEnumerable<UnfairVotingSuspect>? suspects,
        string? contestId)
    {
        var templateText = string.IsNullOrWhiteSpace(template)
            ? DefaultSanctionWarningMessage
            : template.Trim();

        var targetIds = userIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (targetIds.Count == 0)
            return 0;

        var users = await _context.Users
            .Where(u => u.IsActive)
            .Where(u => u.Role >= UserRole.Reader)
            .Where(u => targetIds.Contains(u.Id))
            .ToListAsync();

        if (users.Count == 0)
            return 0;

        var suspectsByUserId = (suspects ?? Enumerable.Empty<UnfairVotingSuspect>())
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var now = DateTime.Now;
        var sender = string.IsNullOrWhiteSpace(createdBy) ? "admin" : createdBy;
        var title = "Предупреждение о санкциях";
        var safeContestId = string.IsNullOrWhiteSpace(contestId) ? string.Empty : contestId.Trim();

        var notifications = users.Select(user =>
        {
            suspectsByUserId.TryGetValue(user.Id, out var suspect);
            var reason = suspect?.Reason ?? "подозрительная активность по результатам проверки";
            var risk = suspect?.RiskScore ?? 0m;
            var message = BuildSanctionsWarningMessage(templateText, user.Username, reason, risk);

            return new UserSanctionNotification
            {
                UserId = user.Id,
                Username = user.Username,
                Title = title,
                Message = message,
                IsRead = false,
                CreatedBy = sender,
                CreatedAt = now
            };
        }).ToList();

        var audits = users.Select(user =>
        {
            suspectsByUserId.TryGetValue(user.Id, out var suspect);
            var reason = suspect?.Reason ?? "подозрительная активность по результатам проверки";
            var risk = suspect?.RiskScore ?? 0m;
            var message = BuildSanctionsWarningMessage(templateText, user.Username, reason, risk);

            return new UserSanctionDispatchAudit
            {
                ContestId = safeContestId,
                RecipientUserId = user.Id,
                RecipientUsername = user.Username,
                Reason = reason,
                RiskScore = Math.Round(risk, 2),
                SentBy = sender,
                TemplateText = templateText,
                RenderedMessage = message,
                SentAt = now
            };
        }).ToList();

        await _context.UserSanctionNotifications.AddRangeAsync(notifications);
        await _context.UserSanctionDispatchAudits.AddRangeAsync(audits);
        await _context.SaveChangesAsync();
        return notifications.Count;
    }

    public async Task<List<UserSanctionDispatchAudit>> GetSanctionsDispatchAuditAsync(string? contestId, int take = 50)
    {
        var safeTake = Math.Clamp(take, 1, 200);
        var query = _context.UserSanctionDispatchAudits.AsQueryable();

        if (!string.IsNullOrWhiteSpace(contestId))
        {
            var normalizedContestId = contestId.Trim();
            query = query.Where(x => x.ContestId == normalizedContestId);
        }

        return await query
            .OrderByDescending(x => x.SentAt)
            .Take(safeTake)
            .ToListAsync();
    }

    public async Task<bool> SaveUnfairVotingSettingsAsync(
        string contestId,
        decimal riskThreshold,
        int minVotesForAnalysis,
        decimal selfVoteWeight,
        decimal extremesWeight,
        decimal favoritismWeight)
    {
        var contest = await _context.Contests.FirstOrDefaultAsync(c => c.Id == contestId);
        if (contest == null)
            return false;

        contest.UnfairVotingDetectionThreshold = ClampDecimal(riskThreshold, 0m, 10m);
        contest.UnfairVotingMinVotesForAnalysis = Math.Clamp(minVotesForAnalysis, 1, 100);
        contest.UnfairVotingSelfVoteRiskWeight = ClampDecimal(selfVoteWeight, 0m, 10m);
        contest.UnfairVotingExtremesRiskWeight = ClampDecimal(extremesWeight, 0m, 10m);
        contest.UnfairVotingFavoritismRiskWeight = ClampDecimal(favoritismWeight, 0m, 10m);
        contest.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<UnfairVotingSuspect>> DetectPotentialUnfairVotingAsync(string contestId)
    {
        var contest = await _context.Contests.FirstOrDefaultAsync(c => c.Id == contestId);
        if (contest == null)
            return new List<UnfairVotingSuspect>();

        var minVotesForAnalysis = Math.Clamp(contest.UnfairVotingMinVotesForAnalysis, 1, 100);
        var selfVoteWeight = ClampDecimal(contest.UnfairVotingSelfVoteRiskWeight, 0m, 10m);
        var extremesWeight = ClampDecimal(contest.UnfairVotingExtremesRiskWeight, 0m, 10m);
        var favoritismWeight = ClampDecimal(contest.UnfairVotingFavoritismRiskWeight, 0m, 10m);

        var votes = await _context.ContestVotes
            .Where(v => v.ContestId == contestId)
            .ToListAsync();

        if (votes.Count == 0)
            return new List<UnfairVotingSuspect>();

        var users = await _context.Users
            .Where(u => u.Role >= UserRole.Reader)
            .ToListAsync();
        var usersById = users.ToDictionary(u => u.Id, u => u);

        var approvedSubmissions = await _context.Submissions
            .Where(s => s.ContestId == contestId && s.Status == WorkStatus.Approved)
            .ToListAsync();
        var authorBySubmissionId = approvedSubmissions
            .GroupBy(s => s.Id)
            .ToDictionary(
                g => g.Key,
                g => NormalizeIdentity(g.First().Work.Author));

        var suspects = new List<UnfairVotingSuspect>();
        foreach (var voterGroup in votes.GroupBy(v => v.VoterUserId))
        {
            if (voterGroup.Key.StartsWith("system:", StringComparison.OrdinalIgnoreCase))
                continue;

            var voterVotes = voterGroup.ToList();
            if (voterVotes.Count < minVotesForAnalysis)
                continue;

            usersById.TryGetValue(voterGroup.Key, out var user);
            var normalizedUsername = NormalizeIdentity(user?.Username ?? voterGroup.First().VoterUsername);
            var reasons = new List<string>();
            var risk = 0m;

            var selfVotesCount = voterVotes.Count(v =>
                authorBySubmissionId.TryGetValue(v.SubmissionId, out var author) &&
                author.Length > 0 &&
                string.Equals(author, normalizedUsername, StringComparison.OrdinalIgnoreCase));
            if (selfVotesCount > 0)
            {
                reasons.Add($"самооценка: {selfVotesCount}");
                risk += selfVoteWeight;
            }

            var maxScore = voterVotes.Max(v => v.Score);
            var minScore = voterVotes.Min(v => v.Score);
            var distinctScores = voterVotes.Select(v => v.Score).Distinct().Count();
            var maxRatio = voterVotes.Count(v => v.Score == maxScore) / (decimal)voterVotes.Count;
            var minRatio = voterVotes.Count(v => v.Score == minScore) / (decimal)voterVotes.Count;

            if (distinctScores <= 2 && (maxRatio >= 0.75m || minRatio >= 0.75m))
            {
                reasons.Add("экстремальные оценки почти без середины");
                risk += extremesWeight;
            }

            var byAuthor = voterVotes
                .Select(v => new
                {
                    Vote = v,
                    Author = authorBySubmissionId.TryGetValue(v.SubmissionId, out var author) ? author : string.Empty
                })
                .Where(x => x.Author.Length > 0)
                .GroupBy(x => x.Author)
                .Select(g => new
                {
                    Author = g.Key,
                    Count = g.Count(),
                    Average = g.Average(x => x.Vote.Score)
                })
                .OrderByDescending(x => x.Count)
                .ThenByDescending(x => x.Average)
                .ToList();

            if (byAuthor.Count >= 2)
            {
                var top = byAuthor[0];
                var otherAverage = byAuthor.Skip(1).Average(x => x.Average);
                var targetRatio = top.Count / (decimal)byAuthor.Sum(x => x.Count);
                if (targetRatio >= 0.6m && top.Average - otherAverage >= 1.5)
                {
                    reasons.Add("перекос оценок в пользу одного автора");
                    risk += favoritismWeight;
                }
            }

            if (reasons.Count == 0)
                continue;

            suspects.Add(new UnfairVotingSuspect
            {
                UserId = voterGroup.Key,
                Username = user?.Username ?? voterGroup.First().VoterUsername,
                DisplayName = user?.DisplayName ?? voterGroup.First().VoterUsername,
                Reason = string.Join("; ", reasons),
                RiskScore = Math.Round(risk, 2)
            });
        }

        return suspects
            .OrderByDescending(x => x.RiskScore)
            .ThenBy(x => x.Username)
            .ToList();
    }

    public async Task<List<UserSanctionNotification>> GetUserSanctionNotificationsAsync(string userId)
    {
        return await _context.UserSanctionNotifications
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.IsRead)
            .ThenByDescending(x => x.CreatedAt)
            .Take(50)
            .ToListAsync();
    }

    public async Task<bool> MarkUserNotificationAsReadAsync(int notificationId, string userId)
    {
        var notification = await _context.UserSanctionNotifications
            .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId);
        if (notification == null)
            return false;

        if (notification.IsRead)
            return true;

        notification.IsRead = true;
        notification.ReadAt = DateTime.Now;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetContestStageAsync(string contestId, ContestStage stage, string changedBy = "manual")
    {
        var contest = await _context.Contests.FirstOrDefaultAsync(c => c.Id == contestId);
        if (contest == null)
            return false;

        var oldStage = GetContestStage(contest);
        var appliedAdminVotes = 0;
        contest.Stage = (int)stage;
        contest.StageUpdatedAt = DateTime.Now;
        contest.UpdatedAt = DateTime.Now;

        if (oldStage == ContestStage.VotingOpen && stage == ContestStage.VotingClosed)
        {
            appliedAdminVotes = await ApplyAdminAverageVotingForContestOnCloseAsync(contest);
        }

        if (stage == ContestStage.Finished)
        {
            contest.IsActive = false;
            contest.ClosedAt ??= DateTime.Now;
        }
        else
        {
            contest.IsActive = true;
        }

        if (oldStage != stage)
        {
            await AddTimelineEventAsync(
                contestId,
                "manual",
                oldStage,
                stage,
                changedBy,
                $"Ручное переключение: {GetContestStageTitle(oldStage)} -> {GetContestStageTitle(stage)}");
        }

            if (appliedAdminVotes > 0)
            {
                await AddTimelineEventAsync(
                contestId,
                "admin-auto-vote",
                stage,
                stage,
                "system",
                $"Автоголосование администрации: обновлено {appliedAdminVotes} оценок по среднему баллу.");
            }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<(bool success, string message)> RollbackContestStageAsync(
        string contestId,
        string changedBy,
        string reason,
        int? rollbackWindowHours = null)
    {
        var contest = await _context.Contests.FirstOrDefaultAsync(c => c.Id == contestId);
        if (contest == null)
            return (false, "Конкурс не найден.");

        var configuredWindow = contest.RollbackWindowHours <= 0 ? 5 : contest.RollbackWindowHours;
        var normalizedWindowHours = Math.Clamp(rollbackWindowHours ?? configuredWindow, 1, 168);
        var stageSince = contest.StageUpdatedAt == default ? contest.StartedAt : contest.StageUpdatedAt;
        var elapsed = DateTime.Now - stageSince;
        var rollbackWindow = TimeSpan.FromHours(normalizedWindowHours);
        if (elapsed > rollbackWindow)
        {
            return (false,
                $"Откат недоступен: срок {normalizedWindowHours} ч истек. " +
                $"С момента входа в текущую стадию прошло {Math.Floor(elapsed.TotalHours)} ч {elapsed.Minutes} мин.");
        }

        var current = GetContestStage(contest);
        var previous = GetPreviousStage(current);
        if (!previous.HasValue)
            return (false, "Откат невозможен: это уже начальная стадия конкурса.");

        var now = DateTime.Now;
        var wasAutoEnabled = contest.AutoStageSwitchEnabled;
        contest.AutoStageSwitchEnabled = false;
        contest.Stage = (int)previous.Value;
        contest.StageUpdatedAt = now;
        contest.UpdatedAt = now;
        contest.LastManualRollbackAt = now;
        contest.IsActive = previous.Value != ContestStage.Finished;

        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? "Запоздалые действия участников"
            : reason.Trim();

        if (wasAutoEnabled)
        {
            await AddTimelineEventAsync(
                contest.Id,
                "mode",
                current,
                current,
                string.IsNullOrWhiteSpace(changedBy) ? "manual" : changedBy,
                "Автоматика отключена из-за ручного отката стадии.");
        }

        await AddTimelineEventAsync(
            contest.Id,
            "rollback",
            current,
            previous.Value,
            string.IsNullOrWhiteSpace(changedBy) ? "manual" : changedBy,
            $"Откат стадии: {GetContestStageTitle(current)} -> {GetContestStageTitle(previous.Value)}. Причина: {normalizedReason}.");

        var topicsCount = await _context.Topics.CountAsync(t => t.ContestId == contestId);
        var submissionsCount = await _context.Submissions.CountAsync(w => w.ContestId == contestId);
        var votesCount = await _context.ContestVotes.CountAsync(v => v.ContestId == contestId);

        await _context.SaveChangesAsync();

        return (true,
            $"Сделан откат на стадию '{GetContestStageTitle(previous.Value)}'. " +
            $"Действия текущего этапа сохранены: тем {topicsCount}, работ {submissionsCount}, голосов {votesCount}.");
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
        string votingClosedSwitchTime,
        int rollbackWindowHours,
        bool autoTopicAssignmentEnabled,
        int autoTopicAssignmentTargetCount,
        bool autoFairVotingEnabled,
        bool autoAdminAverageVotingOnCloseEnabled)
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
        contest.RollbackWindowHours = Math.Clamp(rollbackWindowHours, 1, 168);
        contest.AutoTopicAssignmentEnabled = autoTopicAssignmentEnabled;
        contest.AutoTopicAssignmentTargetCount = Math.Clamp(autoTopicAssignmentTargetCount, 1, 30);
        contest.AutoFairVotingEnabled = autoFairVotingEnabled;
        contest.AutoAdminAverageVotingOnCloseEnabled = autoAdminAverageVotingOnCloseEnabled;
        contest.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetStageManagementModeAsync(string contestId, bool enableAuto, string changedBy)
    {
        var contest = await _context.Contests.FirstOrDefaultAsync(c => c.Id == contestId);
        if (contest == null)
            return false;

        if (contest.AutoStageSwitchEnabled == enableAuto)
            return true;

        contest.AutoStageSwitchEnabled = enableAuto;
        contest.UpdatedAt = DateTime.Now;

        var modeText = enableAuto ? "автоматический" : "ручной";
        await AddTimelineEventAsync(
            contest.Id,
            "mode",
            GetContestStage(contest),
            GetContestStage(contest),
            string.IsNullOrWhiteSpace(changedBy) ? "manual" : changedBy,
            $"Режим ведения переключен на {modeText}.");

        if (enableAuto)
        {
            var current = GetContestStage(contest);
            var next = GetAutoStageForMoment(contest, DateTime.Now);
            if (next != current)
            {
                contest.Stage = (int)next;
                contest.StageUpdatedAt = DateTime.Now;
                contest.UpdatedAt = DateTime.Now;
                if (next == ContestStage.Finished)
                {
                    contest.IsActive = false;
                    contest.ClosedAt ??= DateTime.Now;
                }

                await AddTimelineEventAsync(
                    contest.Id,
                    "auto",
                    current,
                    next,
                    "system",
                    $"Авто-выравнивание стадии при включении автоматики: {GetContestStageTitle(current)} -> {GetContestStageTitle(next)}");
            }
        }

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
            // Защита от гонок: если был ручной откат менее 2 минут назад, пропустить конкурс до следующего цикла.
            if (contest.LastManualRollbackAt.HasValue &&
                (now - contest.LastManualRollbackAt.Value).TotalMinutes < 2)
            {
                continue;
            }

            var currentStage = GetContestStage(contest);
            var next = GetAutoStageForMoment(contest, now);
            if (next == currentStage)
                continue;

            var appliedAdminVotes = 0;
            if (currentStage == ContestStage.VotingOpen && next == ContestStage.VotingClosed)
            {
                appliedAdminVotes = await ApplyAdminAverageVotingForContestOnCloseAsync(contest);
            }

            contest.Stage = (int)next;
            contest.StageUpdatedAt = now;
            contest.UpdatedAt = now;
            if (next == ContestStage.Finished)
            {
                contest.IsActive = false;
                contest.ClosedAt ??= now;
            }

            await AddTimelineEventAsync(
                contest.Id,
                "auto",
                currentStage,
                next,
                "system",
                $"Авто-переключение по расписанию: {GetContestStageTitle(currentStage)} -> {GetContestStageTitle(next)}");

            if (appliedAdminVotes > 0)
            {
                await AddTimelineEventAsync(
                    contest.Id,
                    "admin-auto-vote",
                    next,
                    next,
                    "system",
                    $"Автоголосование администрации: обновлено {appliedAdminVotes} оценок по среднему баллу.");
            }

            changed++;
        }

        if (changed > 0)
            await _context.SaveChangesAsync();

        return changed;
    }

    public async Task<int> ApplyAutomaticTopicAssignmentAsync()
    {
        var now = DateTime.Now;
        var contests = await _context.Contests
            .Where(c => c.AutoTopicAssignmentEnabled)
            .Where(c => c.IsActive)
            .ToListAsync();

        var topicKinds = await _context.TopicKinds
            .OrderBy(k => k.SortNo)
            .ThenBy(k => k.Name)
            .ToListAsync();
        var defaultKindId = topicKinds.FirstOrDefault()?.Id;

        var changed = 0;
        foreach (var contest in contests)
        {
            // Защита от гонок: если был ручной откат менее 2 минут назад, пропустить конкурс до следующего цикла.
            if (contest.LastManualRollbackAt.HasValue &&
                (now - contest.LastManualRollbackAt.Value).TotalMinutes < 2)
            {
                continue;
            }

            if (GetContestStage(contest) != ContestStage.TopicReception)
                continue;

            var existingCount = await _context.Topics.CountAsync(t => t.ContestId == contest.Id);
            var targetCount = Math.Clamp(contest.AutoTopicAssignmentTargetCount, 1, 30);
            if (existingCount >= targetCount)
                continue;

            var needCount = targetCount - existingCount;
            var winnerSuggestions = await GetPreviousContestWinnerTopicSuggestionsAsync(contest.Id, take: targetCount * 2);
            var fallback = GetFallbackAutoTopics();
            var autoTopics = winnerSuggestions
                .Concat(fallback)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(needCount)
                .ToList();

            if (autoTopics.Count == 0)
                continue;

            var (addedWinner, addedRegular, _) = await AddTopicsWithPriorityAsync(
                contest.Id,
                Array.Empty<string>(),
                autoTopics,
                "system:auto-topic",
                null,
                defaultKindId);

            var added = addedWinner + addedRegular;
            if (added <= 0)
                continue;

            await AddTimelineEventAsync(
                contest.Id,
                "auto-topic",
                ContestStage.TopicReception,
                ContestStage.TopicReception,
                "system",
                $"Автоназначение тем: добавлено {added} тем(ы) до целевого количества {targetCount}.");

            await _context.SaveChangesAsync();
            changed++;
        }

        return changed;
    }

    public async Task<int> ApplyAutomaticFairVotingAsync()
    {
        var now = DateTime.Now;
        var contests = await _context.Contests
            .Where(c => c.AutoFairVotingEnabled)
            .Where(c => c.IsActive)
            .Where(c => c.Stage == (int)ContestStage.VotingOpen)
            .ToListAsync();

        var changed = 0;
        foreach (var contest in contests)
        {
            // Защита от гонок: если был ручной откат менее 2 минут назад, пропустить конкурс до следующего цикла.
            if (contest.LastManualRollbackAt.HasValue &&
                (now - contest.LastManualRollbackAt.Value).TotalMinutes < 2)
            {
                continue;
            }

            var submissions = await _context.Submissions
                .Where(s => s.ContestId == contest.Id && s.Status == WorkStatus.Approved)
                .OrderBy(s => s.SubmittedAt)
                .ThenBy(s => s.Id)
                .ToListAsync();

            if (submissions.Count == 0)
                continue;

            var contestVotes = await _context.ContestVotes
                .Where(v => v.ContestId == contest.Id)
                .ToListAsync();

            var minScore = contest.AllowZeroVotes ? 0 : Math.Clamp(contest.BaseVote <= 0 ? 1 : contest.BaseVote, 1, contest.MaxVote);
            var maxScore = contest.MaxVote < minScore ? minScore : contest.MaxVote;
            var updatedVotes = 0;

            foreach (var submission in submissions)
            {
                var humanVotes = contestVotes
                    .Where(v => v.SubmissionId == submission.Id && !string.Equals(v.VoterUserId, FairVotingSystemUserId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                int fairScore;
                if (humanVotes.Count == 0)
                {
                    fairScore = minScore;
                }
                else
                {
                    var avg = humanVotes.Average(v => v.Score);
                    fairScore = Math.Clamp((int)Math.Round(avg, MidpointRounding.AwayFromZero), minScore, maxScore);
                }

                var existingFairVote = contestVotes.FirstOrDefault(v =>
                    string.Equals(v.SubmissionId, submission.Id, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(v.VoterUserId, FairVotingSystemUserId, StringComparison.OrdinalIgnoreCase));

                if (existingFairVote == null)
                {
                    var vote = new ContestVote
                    {
                        ContestId = contest.Id,
                        SubmissionId = submission.Id,
                        VoterUserId = FairVotingSystemUserId,
                        VoterUsername = FairVotingSystemUsername,
                        Score = fairScore,
                        Comment = FairVotingComment,
                        UpdatedAt = DateTime.Now
                    };

                    _context.ContestVotes.Add(vote);
                    contestVotes.Add(vote);
                    updatedVotes++;
                    continue;
                }

                if (existingFairVote.Score == fairScore &&
                    string.Equals(existingFairVote.Comment, FairVotingComment, StringComparison.Ordinal))
                {
                    continue;
                }

                existingFairVote.Score = fairScore;
                existingFairVote.Comment = FairVotingComment;
                existingFairVote.UpdatedAt = DateTime.Now;
                _context.ContestVotes.Update(existingFairVote);
                updatedVotes++;
            }

            if (updatedVotes <= 0)
                continue;

            await AddTimelineEventAsync(
                contest.Id,
                "fair-vote",
                ContestStage.VotingOpen,
                ContestStage.VotingOpen,
                "system",
                $"Честное голосование: обновлено {updatedVotes} автооценок.");

            await _context.SaveChangesAsync();
            changed++;
        }

        return changed;
    }

    public async Task<List<ContestStageTimelineEvent>> GetContestStageTimelineAsync(string contestId, bool withManualAlarm = true)
    {
        if (withManualAlarm)
            await EnsureManualModeAlarmAsync(contestId);

        return await _context.ContestStageTimelineEvents
            .Where(x => x.ContestId == contestId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync();
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
            .OrderBy(t => t.SubmittedAt)
            .ThenBy(t => t.Number)
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
            .OrderBy(t => t.SubmittedAt)
            .ThenBy(t => t.Number)
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
            .OrderBy(t => t.SubmittedAt)
            .ThenBy(t => t.Number)
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

    private static string NormalizeIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = string.Join(' ', value
            .Trim()
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));

        return normalized.ToLowerInvariant();
    }

    private static string BuildSanctionsWarningMessage(string template, string username, string reason, decimal risk)
    {
        var safeUsername = string.IsNullOrWhiteSpace(username) ? "участник" : username.Trim();
        var safeReason = string.IsNullOrWhiteSpace(reason) ? "подозрительная активность" : reason.Trim();
        var safeRisk = risk.ToString("0.00");

        return template
            .Replace("{username}", safeUsername, StringComparison.OrdinalIgnoreCase)
            .Replace("{reason}", safeReason, StringComparison.OrdinalIgnoreCase)
            .Replace("{risk}", safeRisk, StringComparison.OrdinalIgnoreCase);
    }

    private static decimal ClampDecimal(decimal value, decimal min, decimal max)
    {
        if (value < min)
            return min;
        if (value > max)
            return max;

        return value;
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

    private static ContestStage? GetPreviousStage(ContestStage stage)
    {
        return stage switch
        {
            ContestStage.TopicReception => null,
            ContestStage.WorkReception => ContestStage.TopicReception,
            ContestStage.VotingOpen => ContestStage.WorkReception,
            ContestStage.VotingClosed => ContestStage.VotingOpen,
            ContestStage.Finished => ContestStage.VotingClosed,
            _ => null
        };
    }

    private static IEnumerable<string> GetFallbackAutoTopics()
    {
        return new[]
        {
            "Письмо без адреса",
            "Дорога домой",
            "Свет в окне",
            "Тишина после грозы",
            "Город на рассвете",
            "Чужой календарь",
            "Остановка времени",
            "Следы на снегу",
            "Память воды",
            "Последний трамвай"
        };
    }

    private async Task<int> ApplyAdminAverageVotingForContestOnCloseAsync(Contest contest)
    {
        if (!contest.AutoAdminAverageVotingOnCloseEnabled)
            return 0;

        var submissions = await _context.Submissions
            .Where(s => s.ContestId == contest.Id && s.Status == WorkStatus.Approved)
            .OrderBy(s => s.SubmittedAt)
            .ThenBy(s => s.Id)
            .ToListAsync();

        if (submissions.Count == 0)
            return 0;

        var votes = await _context.ContestVotes
            .Where(v => v.ContestId == contest.Id)
            .ToListAsync();

        var minScore = contest.AllowZeroVotes ? 0 : Math.Clamp(contest.BaseVote <= 0 ? 1 : contest.BaseVote, 1, contest.MaxVote);
        var maxScore = contest.MaxVote < minScore ? minScore : contest.MaxVote;
        var changed = 0;

        foreach (var submission in submissions)
        {
            var sourceVotes = votes
                .Where(v => v.SubmissionId == submission.Id)
                .Where(v => !string.Equals(v.VoterUserId, AdminAverageSystemUserId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (sourceVotes.Count == 0)
                continue;

            var average = sourceVotes.Average(v => v.Score);
            var score = Math.Clamp((int)Math.Round(average, MidpointRounding.AwayFromZero), minScore, maxScore);

            var existing = votes.FirstOrDefault(v =>
                string.Equals(v.SubmissionId, submission.Id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(v.VoterUserId, AdminAverageSystemUserId, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                var vote = new ContestVote
                {
                    ContestId = contest.Id,
                    SubmissionId = submission.Id,
                    VoterUserId = AdminAverageSystemUserId,
                    VoterUsername = AdminAverageSystemUsername,
                    Score = score,
                    Comment = AdminAverageComment,
                    UpdatedAt = DateTime.Now
                };

                _context.ContestVotes.Add(vote);
                votes.Add(vote);
                changed++;
                continue;
            }

            if (existing.Score == score && string.Equals(existing.Comment, AdminAverageComment, StringComparison.Ordinal))
                continue;

            existing.Score = score;
            existing.Comment = AdminAverageComment;
            existing.UpdatedAt = DateTime.Now;
            _context.ContestVotes.Update(existing);
            changed++;
        }

        return changed;
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

    private async Task EnsureManualModeAlarmAsync(string contestId)
    {
        var contest = await _context.Contests.FirstOrDefaultAsync(c => c.Id == contestId);
        if (contest == null || contest.AutoStageSwitchEnabled)
            return;

        var stage = GetContestStage(contest);
        if (stage == ContestStage.Finished)
            return;

        var anchor = contest.StageUpdatedAt == default ? contest.StartedAt : contest.StageUpdatedAt;
        var hasSchedule = stage switch
        {
            ContestStage.TopicReception => TryGetScheduledSwitchMoment(anchor, contest.TopicReceptionSwitchDayOfWeek, contest.TopicReceptionSwitchTime, out var topicSwitch) && CreateAlarmIfOverdue(contest, stage, topicSwitch),
            ContestStage.WorkReception => TryGetScheduledSwitchMoment(anchor, contest.WorkReceptionSwitchDayOfWeek, contest.WorkReceptionSwitchTime, out var workSwitch) && CreateAlarmIfOverdue(contest, stage, workSwitch),
            ContestStage.VotingOpen => TryGetScheduledSwitchMoment(anchor, contest.VotingOpenSwitchDayOfWeek, contest.VotingOpenSwitchTime, out var votingOpenSwitch) && CreateAlarmIfOverdue(contest, stage, votingOpenSwitch),
            ContestStage.VotingClosed => TryGetScheduledSwitchMoment(anchor, contest.VotingClosedSwitchDayOfWeek, contest.VotingClosedSwitchTime, out var votingClosedSwitch) && CreateAlarmIfOverdue(contest, stage, votingClosedSwitch),
            _ => false
        };

        if (hasSchedule)
            await _context.SaveChangesAsync();
    }

    private bool CreateAlarmIfOverdue(Contest contest, ContestStage stage, DateTime scheduledSwitch)
    {
        if (DateTime.Now < scheduledSwitch)
            return false;

        var alarmKey = $"manual-overdue:{contest.Id}:{(int)stage}:{scheduledSwitch:yyyyMMddHHmm}";
        var exists = _context.ContestStageTimelineEvents
            .Any(x => x.AlarmKey == alarmKey);
        if (exists)
            return false;

        _context.ContestStageTimelineEvents.Add(new ContestStageTimelineEvent
        {
            ContestId = contest.Id,
            EventType = "alarm",
            StageFrom = (int)stage,
            StageTo = (int)stage,
            CreatedBy = "system",
            Message = $"Аларм: стадия '{GetContestStageTitle(stage)}' просрочена. Требуется ручное переключение.",
            AlarmKey = alarmKey,
            CreatedAt = DateTime.Now
        });

        return true;
    }

    private async Task AddTimelineEventAsync(
        string contestId,
        string eventType,
        ContestStage stageFrom,
        ContestStage stageTo,
        string createdBy,
        string message)
    {
        _context.ContestStageTimelineEvents.Add(new ContestStageTimelineEvent
        {
            ContestId = contestId,
            EventType = eventType,
            StageFrom = (int)stageFrom,
            StageTo = (int)stageTo,
            CreatedBy = createdBy,
            Message = message,
            CreatedAt = DateTime.Now
        });

        await Task.CompletedTask;
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
