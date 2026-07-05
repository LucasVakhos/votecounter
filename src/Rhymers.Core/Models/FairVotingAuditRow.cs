namespace Rhymers.Core.Models;

public sealed class FairVotingAuditRow
{
    public int DisplayNo { get; init; }
    public string Topic { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public int HumanVotesCount { get; init; }
    public decimal? HumanAverage { get; init; }
    public decimal? FairSystemScore { get; init; }
    public decimal? AdminSystemScore { get; init; }
    public decimal? FairDelta { get; init; }
    public decimal? AdminDelta { get; init; }
    public DateTime? FairSystemUpdatedAt { get; init; }
    public DateTime? AdminSystemUpdatedAt { get; init; }
    public string RuleLabel { get; init; } = string.Empty;
}
