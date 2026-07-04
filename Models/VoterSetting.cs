namespace VoteCounter.Models;

public sealed class VoterSetting
{
    public string Name { get; set; } = string.Empty;
    public bool MustVote { get; set; } = true;
    public bool HasVoted { get; set; }

    public VoterSetting Clone()
    {
        return new VoterSetting { Name = Name, MustVote = MustVote, HasVoted = HasVoted };
    }
}
