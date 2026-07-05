using VoteCounter.Core.Models;

namespace VoteCounter.Core.Services;

/// <summary>
/// Сервис для управления модерацией работ.
/// </summary>
/// <remarks>
/// Управляет процессом подачи работ и их одобрением/отклонением.
/// </remarks>
public sealed class ModerationService
{
    private readonly Dictionary<string, List<WorkSubmission>> _submissions = new();

    /// <summary>
    /// Подать новую работу на модерацию.
    /// </summary>
    /// <param name="contestId">ID конкурса</param>
    /// <param name="work">Работа для подачи</param>
    /// <returns>Подача с присвоенным ID</returns>
    public WorkSubmission SubmitWork(string contestId, ContestWork work)
    {
        if (string.IsNullOrEmpty(contestId))
            throw new ArgumentException("Contest ID cannot be empty", nameof(contestId));
        
        if (work == null)
            throw new ArgumentNullException(nameof(work));

        var submission = new WorkSubmission
        {
            Id = Guid.NewGuid().ToString("N"),
            ContestId = contestId,
            Work = work.Clone(),
            Status = WorkStatus.PendingModeration,
            SubmittedAt = DateTime.Now
        };

        if (!_submissions.ContainsKey(contestId))
        {
            _submissions[contestId] = new List<WorkSubmission>();
        }

        _submissions[contestId].Add(submission);
        return submission;
    }

    /// <summary>
    /// Получить очередь работ на модерацию.
    /// </summary>
    /// <param name="contestId">ID конкурса</param>
    /// <returns>Список неодобренных работ</returns>
    public List<WorkSubmission> GetPendingSubmissions(string contestId)
    {
        if (!_submissions.ContainsKey(contestId))
            return new List<WorkSubmission>();

        return _submissions[contestId]
            .Where(s => s.Status == WorkStatus.PendingModeration || s.Status == WorkStatus.UnderReview)
            .OrderBy(s => s.SubmittedAt)
            .ToList();
    }

    /// <summary>
    /// Получить все подачи конкурса.
    /// </summary>
    public List<WorkSubmission> GetAllSubmissions(string contestId)
    {
        if (!_submissions.ContainsKey(contestId))
            return new List<WorkSubmission>();

        return _submissions[contestId].OrderByDescending(s => s.SubmittedAt).ToList();
    }

    /// <summary>
    /// Одобрить работу и добавить в конкурс.
    /// </summary>
    /// <param name="contestId">ID конкурса</param>
    /// <param name="submissionId">ID подачи</param>
    /// <param name="moderatorName">Имя модератора</param>
    /// <returns>true если успешно, false если подача не найдена</returns>
    public bool ApproveSubmission(string contestId, string submissionId, string moderatorName = "System")
    {
        var submission = GetSubmissionById(contestId, submissionId);
        if (submission == null)
            return false;

        submission.Status = WorkStatus.Approved;
        submission.ModeratedAt = DateTime.Now;
        submission.ModeratorName = moderatorName;
        submission.Work.Status = WorkStatus.Approved;

        return true;
    }

    /// <summary>
    /// Отклонить работу.
    /// </summary>
    /// <param name="contestId">ID конкурса</param>
    /// <param name="submissionId">ID подачи</param>
    /// <param name="reason">Причина отклонения</param>
    /// <param name="moderatorName">Имя модератора</param>
    /// <returns>true если успешно, false если подача не найдена</returns>
    public bool RejectSubmission(string contestId, string submissionId, string reason, string moderatorName = "System")
    {
        var submission = GetSubmissionById(contestId, submissionId);
        if (submission == null)
            return false;

        submission.Status = WorkStatus.Rejected;
        submission.ModeratedAt = DateTime.Now;
        submission.ModeratorName = moderatorName;
        submission.ModerationNote = reason;
        submission.Work.Status = WorkStatus.Rejected;

        return true;
    }

    /// <summary>
    /// Отправить работу на повторное рассмотрение.
    /// </summary>
    public bool SendForReview(string contestId, string submissionId)
    {
        var submission = GetSubmissionById(contestId, submissionId);
        if (submission == null)
            return false;

        submission.Status = WorkStatus.UnderReview;
        return true;
    }

    /// <summary>
    /// Получить подачу по ID.
    /// </summary>
    private WorkSubmission? GetSubmissionById(string contestId, string submissionId)
    {
        if (!_submissions.ContainsKey(contestId))
            return null;

        return _submissions[contestId].FirstOrDefault(s => s.Id == submissionId);
    }

    /// <summary>
    /// Получить одобренные работы для конкурса.
    /// </summary>
    public List<ContestWork> GetApprovedWorks(string contestId)
    {
        if (!_submissions.ContainsKey(contestId))
            return new List<ContestWork>();

        return _submissions[contestId]
            .Where(s => s.Status == WorkStatus.Approved)
            .Select(s => s.Work)
            .OrderBy(w => w.Number)
            .ToList();
    }
}
