namespace Rhymers.Core.Models;

public sealed class VoterStatusRow
{
    public string VoterName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool RequiredToVote { get; set; }
    public int AcceptedVotes { get; set; }
    public int KnownVotes { get; set; }
    public int MissingCount { get; set; }
    public string MissingWorks { get; set; } = string.Empty;
    public string UnknownWorks { get; set; } = string.Empty;
    public int SelfVotes { get; set; }
    public DateTime? LastVoteAt { get; set; }
    public string Note { get; set; } = string.Empty;

    public bool IsDebtor => RequiredToVote && (AcceptedVotes == 0 || MissingCount > 0);
    public bool IsUnknownVoter => Status.Contains("не в списке", StringComparison.OrdinalIgnoreCase);
}
