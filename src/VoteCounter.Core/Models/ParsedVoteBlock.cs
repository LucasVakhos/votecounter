namespace VoteCounter.Core.Models;

/// <summary>
/// Represents a block of votes from a single voter.
/// </summary>
/// <remarks>
/// Groups multiple vote entries that belong to one voter.
/// Used during parsing to collect votes before validation.
/// </remarks>
public sealed class ParsedVoteBlock
{
    public string VoterName { get; set; } = "";
    public List<VoteEntry> Votes { get; } = new();
}

/// <summary>
/// Result of parsing vote text, containing blocks and validation warnings.
/// </summary>
/// <remarks>
/// Contains parsed vote blocks from all voters and any warnings encountered during parsing.
/// </remarks>
public sealed class ImportResult
{
    public List<ParsedVoteBlock> Blocks { get; } = new();
    public List<string> Warnings { get; } = new();
    public int VoteCount => Blocks.Sum(x => x.Votes.Count);
    public int VoterCount => Blocks.Count;
}
