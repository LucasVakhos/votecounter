namespace Rhymers.Core.Models;

public sealed class ContestTopic
{
    public string ContestId { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? TopicKindId { get; set; }
    public string ProposedBy { get; set; } = string.Empty;
    public bool IsWinnerTopic { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.Now;

    public ContestTopic Clone()
    {
        return new ContestTopic
        {
            ContestId = ContestId,
            Number = Number,
            Title = Title,
            TopicKindId = TopicKindId,
            ProposedBy = ProposedBy,
            IsWinnerTopic = IsWinnerTopic,
            SubmittedAt = SubmittedAt
        };
    }
}
