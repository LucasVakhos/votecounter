namespace VoteCounter.Models;

public sealed class ParsedVoteBlock
{
    public string VoterName { get; set; } = "";
    public List<VoteEntry> Votes { get; } = new();
}

public sealed class ImportResult
{
    public List<ParsedVoteBlock> Blocks { get; } = new();
    public List<string> Warnings { get; } = new();
    public int VoteCount => Blocks.Sum(x => x.Votes.Count);
    public int VoterCount => Blocks.Count;
}
