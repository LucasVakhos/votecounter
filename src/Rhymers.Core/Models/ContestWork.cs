namespace Rhymers.Core.Models;

/// <summary>
/// Represents a single work (entry) submitted to a contest.
/// </summary>
/// <remarks>
/// Contains work metadata including title, author, topic assignment, and status.
/// </remarks>
public sealed class ContestWork
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool HasVotes { get; set; }
    
    /// <summary>Статус работы в процессе модерации</summary>
    public WorkStatus Status { get; set; } = WorkStatus.PendingModeration;
    
    /// <summary>Когда работа была подана</summary>
    public DateTime SubmittedAt { get; set; } = DateTime.Now;

    /// <summary>Информация об авторском праве (обладатель копирайта)</summary>
    public string? CopyrightHolder { get; set; }

    /// <summary>Номер свидетельства о регистрации права собственности</summary>
    public string? RegistrationCertificateNumber { get; set; }

    public ContestWork Clone()
    {
        return new ContestWork
        {
            Number = Number,
            Title = Title,
            Subtitle = Subtitle,
            Author = Author,
            Topic = Topic,
            Content = Content,
            HasVotes = HasVotes,
            Status = Status,
            SubmittedAt = SubmittedAt,
            CopyrightHolder = CopyrightHolder,
            RegistrationCertificateNumber = RegistrationCertificateNumber
        };
    }
}
