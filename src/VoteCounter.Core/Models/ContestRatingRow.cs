namespace VoteCounter.Core.Models;

public sealed class ContestRatingRow
{
    public int PlaceNo { get; set; }
    public int PlaceTo { get; set; }
    public string PlaceText => PlaceNo == PlaceTo ? PlaceNo.ToString() : $"{PlaceNo}-{PlaceTo}";
    public int WorkNo { get; set; }
    public string WorkNoText => WorkNo.ToString("00");
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public bool Accepted { get; set; } = true;
    public bool HasVotes => AcceptedVotes + SelfVotes > 0;
    public string AcceptedText => Accepted ? "Да" : "Нет";
    public string VoteStatusText => HasVotes ? "Да" : "Нет";
    public decimal Rate { get; set; }
    public int AcceptedVotes { get; set; }
    public int SelfVotes { get; set; }
    public int MaxVotes { get; set; }
    public decimal Average { get; set; }
    public string AverageText => AcceptedVotes == 0 ? string.Empty : Average.ToString("0.00");
}
