namespace VoteCounter.Models;

public sealed class ContestWork
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool HasVotes { get; set; }

    public ContestWork Clone()
    {
        return new ContestWork
        {
            Number = Number,
            Title = Title,
            Author = Author,
            Topic = Topic,
            Content = Content,
            HasVotes = HasVotes
        };
    }
}
