namespace Rhymers.Core.Models;

/// <summary>
/// Статус работы в системе модерации.
/// </summary>
public enum WorkStatus
{
    /// <summary>Ожидает модерации</summary>
    PendingModeration = 0,
    
    /// <summary>Одобрена, добавлена в конкурс</summary>
    Approved = 1,
    
    /// <summary>Отклонена модератором</summary>
    Rejected = 2,
    
    /// <summary>На рассмотрении модератором</summary>
    UnderReview = 3
}
