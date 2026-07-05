namespace VoteCounter.Core.Models;

/// <summary>
/// Contains the complete results of a contest including rankings and statistics.
/// </summary>
/// <remarks>
/// Stores rating rows (one per work), voter count, and vote statistics.
/// </remarks>
public sealed class ContestResultsReport
{
    public List<ContestRatingRow> Rows { get; } = new();
    public int WorkCount => Rows.Count;
    public int VoterCount { get; set; }
    public int AcceptedVoteCount { get; set; }
    public int SelfVoteCount { get; set; }
    public decimal TotalRate => Rows.Sum(x => x.Rate);
}
