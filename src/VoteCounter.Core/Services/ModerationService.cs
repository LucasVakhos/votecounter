using VoteCounter.Core.Models;
using VoteCounter.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace VoteCounter.Core.Services;

/// <summary>
/// Сервис для управления модерацией работ.
/// </summary>
/// <remarks>
/// Управляет процессом подачи работ и их одобрением/отклонением.
/// </remarks>
public sealed class ModerationService
{
    private readonly VoteCounterDbContext _context;

    public ModerationService(VoteCounterDbContext context)
    {
        _context = context;
    }

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

        _context.Submissions.Add(submission);
        _context.SaveChanges();
        return submission;
    }

    /// <summary>
    /// Получить очередь работ на модерацию.
    /// </summary>
    /// <param name="contestId">ID конкурса</param>
    /// <returns>Список неодобренных работ</returns>
    public List<WorkSubmission> GetPendingSubmissions(string contestId)
    {
        return _context.Submissions
            .Where(s => s.ContestId == contestId && 
                        (s.Status == WorkStatus.PendingModeration || s.Status == WorkStatus.UnderReview))
            .OrderBy(s => s.SubmittedAt)
            .ToList();
    }

    /// <summary>
    /// Получить все подачи конкурса.
    /// </summary>
    public List<WorkSubmission> GetAllSubmissions(string contestId)
    {
        return _context.Submissions
            .Where(s => s.ContestId == contestId)
            .OrderByDescending(s => s.SubmittedAt)
            .ToList();
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

        _context.Submissions.Update(submission);
        _context.SaveChanges();
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

        _context.Submissions.Update(submission);
        _context.SaveChanges();
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
        _context.Submissions.Update(submission);
        _context.SaveChanges();
        return true;
    }

    /// <summary>
    /// Получить подачу по ID.
    /// </summary>
    private WorkSubmission? GetSubmissionById(string contestId, string submissionId)
    {
        return _context.Submissions.FirstOrDefault(s => s.ContestId == contestId && s.Id == submissionId);
    }

    /// <summary>
    /// Получить одобренные работы для конкурса.
    /// </summary>
    public List<ContestWork> GetApprovedWorks(string contestId)
    {
        return _context.Submissions
            .Where(s => s.ContestId == contestId && s.Status == WorkStatus.Approved)
            .Select(s => s.Work)
            .OrderBy(w => w.Number)
            .ToList();
    }

    /// <summary>
    /// Получить все работы конкретного автора.
    /// </summary>
    /// <param name="authorName">Имя автора</param>
    /// <returns>Все подачи автора со всех конкурсов</returns>
    public List<WorkSubmission> GetAuthorSubmissions(string authorName)
    {
        if (string.IsNullOrEmpty(authorName))
            return new List<WorkSubmission>();

        return _context.Submissions
            .Where(s => s.Work.Author == authorName)
            .OrderByDescending(s => s.SubmittedAt)
            .ToList();
    }
}
