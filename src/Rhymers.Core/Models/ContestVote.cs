namespace Rhymers.Core.Models;

public sealed class ContestVote
{
    public string ContestId { get; set; } = string.Empty;
    public string SubmissionId { get; set; } = string.Empty;
    public string VoterUserId { get; set; } = string.Empty;
    public string VoterUsername { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
