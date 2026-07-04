namespace VoteCounter.Models;

public sealed class ContestResultsReport
{
    public List<ContestRatingRow> Rows { get; } = new();
    public int WorkCount => Rows.Count;
    public int VoterCount { get; set; }
    public int AcceptedVoteCount { get; set; }
    public int SelfVoteCount { get; set; }
    public decimal TotalRate => Rows.Sum(x => x.Rate);
}
