namespace Rhymers.Core.Models;

public sealed class VotePair
{
    public int WorkNo { get; set; }
    public decimal Score { get; set; }
    public string ScoreText { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string SourceLine { get; set; } = string.Empty;
}
