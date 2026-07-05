using Rhymers.Core.Models;
using Rhymers.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Rhymers.Core.Services;

/// <summary>
/// Сервис для управления модерацией работ.
/// </summary>
/// <remarks>
/// Управляет процессом подачи работ и их одобрением/отклонением.
/// </remarks>
public sealed class ModerationService
{
    private readonly RhymersDbContext _context;

    public ModerationService(RhymersDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Подать новую работу на модерацию.
    /// </summary>
    /// <param name="contestId">ID конкурса</param>
    /// <param name="work">Работа для подачи</param>
    /// <returns>Подача с присвоенным ID и номером свидетельства</returns>
    public WorkSubmission SubmitWork(string contestId, ContestWork work)
    {
        if (string.IsNullOrEmpty(contestId))
            throw new ArgumentException("Contest ID cannot be empty", nameof(contestId));
        
        if (work == null)
            throw new ArgumentNullException(nameof(work));

        // Получить конкурс для номера и подсчёта работ
        var contest = _context.Contests.FirstOrDefault(c => c.Id == contestId);
        if (contest == null)
            throw new ArgumentException($"Contest {contestId} not found", nameof(contestId));

        // Подсчитать уже подано работ в этот конкурс (для порядкового номера)
        var submissionCount = _context.Submissions
            .Where(s => s.ContestId == contestId)
            .Count();

        // Сгенерировать номер свидетельства: YYYYMMDD-{номер конкурса}-{порядковый}
        var certNumber = GenerateRegistrationCertificateNumber(
            DateTime.Now, 
            contest.Number, 
            submissionCount + 1
        );

        var workClone = work.Clone();
        workClone.RegistrationCertificateNumber = certNumber;

        var submission = new WorkSubmission
        {
            Id = Guid.NewGuid().ToString("N"),
            ContestId = contestId,
            Work = workClone,
            Status = WorkStatus.PendingModeration,
            SubmittedAt = DateTime.Now
        };

        _context.Submissions.Add(submission);
        _context.SaveChanges();
        return submission;
    }

    /// <summary>
    /// Генерирует номер свидетельства о регистрации права собственности.
    /// Формат: YYYYMMDD-{номер конкурса}-{порядковый номер}
    /// Пример: 20260705-001-001
    /// </summary>
    private static string GenerateRegistrationCertificateNumber(DateTime submittedAt, string contestNumber, int sequentialNumber)
    {
        var dateString = submittedAt.ToString("yyyyMMdd");
        var contestNum = (contestNumber ?? "001").PadLeft(3, '0');
        var sequentialNum = sequentialNumber.ToString().PadLeft(3, '0');
        return $"{dateString}-{contestNum}-{sequentialNum}";
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
