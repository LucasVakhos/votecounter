namespace VoteCounter.Core.Models;

/// <summary>
/// Представляет подачу работы поэтом с информацией о модерации.
/// </summary>
/// <remarks>
/// Содержит работу, её статус, время подачи и примечания модератора.
/// </remarks>
public sealed class WorkSubmission
{
    /// <summary>Уникальный идентификатор подачи</summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>ID конкурса</summary>
    public string ContestId { get; set; } = string.Empty;
    
    /// <summary>Сама работа</summary>
    public ContestWork Work { get; set; } = new();
    
    /// <summary>Статус модерации</summary>
    public WorkStatus Status { get; set; } = WorkStatus.PendingModeration;
    
    /// <summary>Время подачи</summary>
    public DateTime SubmittedAt { get; set; } = DateTime.Now;
    
    /// <summary>Время последней модерации</summary>
    public DateTime? ModeratedAt { get; set; }
    
    /// <summary>Имя модератора</summary>
    public string? ModeratorName { get; set; }
    
    /// <summary>Причина отклонения или примечание</summary>
    public string? ModerationNote { get; set; }
    
    public WorkSubmission Clone()
    {
        return new WorkSubmission
        {
            Id = Id,
            ContestId = ContestId,
            Work = Work.Clone(),
            Status = Status,
            SubmittedAt = SubmittedAt,
            ModeratedAt = ModeratedAt,
            ModeratorName = ModeratorName,
            ModerationNote = ModerationNote
        };
    }
}
