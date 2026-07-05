using VoteCounter.Core.Models;
using VoteCounter.Core.Services;

namespace VoteCounter.Web.Services;

/// <summary>
/// Сервис для работы с модерацией работ в Blazor приложении
/// </summary>
public class ModerationWebService
{
    private readonly ModerationService _moderationService;
    private readonly WorkSpellChecker _spellChecker;

    public ModerationWebService(ModerationService moderationService, WorkSpellChecker spellChecker)
    {
        _moderationService = moderationService;
        _spellChecker = spellChecker;
    }

    /// <summary>
    /// Поэт подает новую работу
    /// </summary>
    public async Task<WorkSubmission> SubmitWorkAsync(string contestId, ContestWork work)
    {
        var submission = _moderationService.SubmitWork(contestId, work);
        return await Task.FromResult(submission);
    }

    /// <summary>
    /// Получить очередь работ для модерации
    /// </summary>
    public async Task<List<WorkSubmission>> GetModerationQueueAsync(string contestId)
    {
        var submissions = _moderationService.GetPendingSubmissions(contestId);
        return await Task.FromResult(submissions);
    }

    /// <summary>
    /// Получить все подачи
    /// </summary>
    public async Task<List<WorkSubmission>> GetAllSubmissionsAsync(string contestId)
    {
        var submissions = _moderationService.GetAllSubmissions(contestId);
        return await Task.FromResult(submissions);
    }

    /// <summary>
    /// Модератор одобряет работу
    /// </summary>
    public async Task<bool> ApproveWorkAsync(string contestId, string submissionId, string moderatorName)
    {
        var result = _moderationService.ApproveSubmission(contestId, submissionId, moderatorName);
        return await Task.FromResult(result);
    }

    /// <summary>
    /// Модератор отклоняет работу
    /// </summary>
    public async Task<bool> RejectWorkAsync(string contestId, string submissionId, string reason, string moderatorName)
    {
        var result = _moderationService.RejectSubmission(contestId, submissionId, reason, moderatorName);
        return await Task.FromResult(result);
    }

    /// <summary>
    /// Получить одобренные работы
    /// </summary>
    public async Task<List<ContestWork>> GetApprovedWorksAsync(string contestId)
    {
        var works = _moderationService.GetApprovedWorks(contestId);
        return await Task.FromResult(works);
    }

    /// <summary>
    /// Проверить грамотность текста
    /// </summary>
    public async Task<WorkSpellCheckReport> CheckGrammarAsync(ContestWork work)
    {
        var report = _spellChecker.CheckWork(work);
        return await Task.FromResult(report);
    }
}
