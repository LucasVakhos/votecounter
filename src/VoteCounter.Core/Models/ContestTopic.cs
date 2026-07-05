namespace VoteCounter.Core.Models;

public sealed class ContestTopic
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;

    public ContestTopic Clone()
    {
        return new ContestTopic
        {
            Number = Number,
            Title = Title
        };
    }
}
