namespace VoteCounter.Models;

public sealed class ContestAuditReport
{
    public List<VoterStatusRow> Rows { get; } = new();
    public int RequiredVoters => Rows.Count(x => x.RequiredToVote && !x.IsUnknownVoter);
    public int CompletedVoters => Rows.Count(x => x.RequiredToVote && !x.IsUnknownVoter && x.AcceptedVotes > 0 && x.MissingCount == 0);
    public int Debtors => Rows.Count(x => x.IsDebtor && !x.IsUnknownVoter);
    public int UnknownVoters => Rows.Count(x => x.IsUnknownVoter);
    public int WorkCount { get; set; }
    public int AcceptedVoteCount { get; set; }
}
