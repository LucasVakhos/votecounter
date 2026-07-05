namespace Rhymers.Core.Models;

public sealed class UnfairVotingSuspect
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal RiskScore { get; set; }
}
