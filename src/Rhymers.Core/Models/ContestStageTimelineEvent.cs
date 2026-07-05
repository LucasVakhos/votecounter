namespace Rhymers.Core.Models;

public sealed class ContestStageTimelineEvent
{
    public int Id { get; set; }
    public string ContestId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public int StageFrom { get; set; }
    public int StageTo { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string AlarmKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
