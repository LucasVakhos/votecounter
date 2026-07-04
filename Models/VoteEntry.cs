namespace VoteCounter.Models;

public sealed class VoteEntry
{
    public string ContestId { get; set; } = "";
    public string VoterName { get; set; } = "";
    public string VoterKey { get; set; } = "";
    public int WorkNo { get; set; }
    public string ScoreText { get; set; } = "";
    public decimal Score { get; set; }
    public decimal OriginalScore { get; set; }
    public string OriginalScoreText { get; set; } = "";
    public decimal VotedScore { get; set; }
    public string VotedScoreText { get; set; } = "";
    public decimal AcceptedScore { get; set; }
    public string AcceptedScoreText { get; set; } = "";
    public bool WasChangedByRules { get; set; }
    public string RuleNote { get; set; } = "";
    public string Comment { get; set; } = "";
    public string SourceLine { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
