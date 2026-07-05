namespace Rhymers.Core.Models;

public sealed class UserSanctionDispatchAudit
{
    public int Id { get; set; }
    public string ContestId { get; set; } = string.Empty;
    public string RecipientUserId { get; set; } = string.Empty;
    public string RecipientUsername { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal RiskScore { get; set; }
    public string SentBy { get; set; } = string.Empty;
    public string TemplateText { get; set; } = string.Empty;
    public string RenderedMessage { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.Now;
}
