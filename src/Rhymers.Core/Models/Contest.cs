namespace Rhymers.Core.Models;

/// <summary>
/// Represents a single contest with voting rules and metadata.
/// </summary>
/// <remarks>
/// Contains contest information including voting rules (vote limits, score ranges),
/// stage management, and participant details.
/// </remarks>
public sealed class Contest
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Number { get; set; } = "001";
    public string Name { get; set; } = "Новый конкурс";
    public int Stage { get; set; } = (int)ContestStage.TopicReception;
    public DateTime StageUpdatedAt { get; set; } = DateTime.Now;
    public string HostName { get; set; } = string.Empty;
    public string NextHostName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.Now;
    public DateTime? ClosedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Правила голосования. Имена максимально близки к старой Firebird/Rhyme Machine:
    // CN_BASE_VOTE / CN_MAX_VOTE / CN_LIMIT_MAX_VOTE / CN_ONE_ON_TOPIC / CN_CAN_NULL_RATE / CN_CAN_VOTE_FOR_SELF.
    public int VoteLimit { get; set; } = 0; // 0 = без ограничения; >0 = принимать только первые N оценок голосующего.
    public int BaseVote { get; set; } = 3;
    public int MaxVote { get; set; } = 4;
    public int LimitMaxVote { get; set; } = 0; // 0 = без ограничения.
    public bool LimitMaxVoteByTopic { get; set; } = false;
    public bool OneMaxVotePerTopic { get; set; } = false;
    public bool DowngradeExtraMaxVoteToBase { get; set; } = true;
    public bool AllowZeroVotes { get; set; } = false;
    public bool TreatSelfVoteAsZero { get; set; } = true;

    // Режим ведения конкурса:
    // true  - таблицу ведёт ведущий, авторы известны и импорт работ может сразу брать автора из строки;
    // false - таблицу ведёт сторонний счётчик до раскрытия, авторы скрыты как "Неизвестный автор".
    public bool HostKnowsAuthors { get; set; } = true;

    // 0 = без ограничения, >0 = максимальное количество тем в конкурсе.
    public int MaxTopicsCount { get; set; } = 0;

    // true = стадии переключаются автоматически по расписанию.
    public bool AutoStageSwitchEnabled { get; set; }

    // Моменты завершения стадий.
    public DateTime? TopicReceptionEndsAt { get; set; }
    public DateTime? WorkReceptionEndsAt { get; set; }
    public DateTime? VotingOpenEndsAt { get; set; }
    public DateTime? VotingClosedEndsAt { get; set; }

    public List<ContestTopic> Topics { get; set; } = new();
    public List<ContestWork> Works { get; set; } = new();
    public List<VoterSetting> Voters { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public override string ToString()
    {
        var number = string.IsNullOrWhiteSpace(Number) ? "без №" : $"№{Number}";
        return $"{number} - {Name}";
    }
}
