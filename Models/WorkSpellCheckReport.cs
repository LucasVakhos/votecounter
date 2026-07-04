namespace VoteCounter.Models;

public sealed class WorkSpellCheckReport
{
    public List<WorkSpellIssue> Issues { get; } = new();
    public bool HasIssues => Issues.Count > 0;
    public bool HasErrors => Issues.Any(x => x.Severity == WorkSpellIssueSeverity.Error);

    public string BuildText()
    {
        if (Issues.Count == 0)
            return "Ошибок и подозрительных мест не найдено.";

        return string.Join(Environment.NewLine, Issues.Select(x => x.ToString()));
    }
}

public sealed class WorkSpellIssue
{
    public WorkSpellIssueSeverity Severity { get; set; }
    public int LineNo { get; set; }
    public string Fragment { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Suggestion { get; set; } = string.Empty;

    public override string ToString()
    {
        string prefix = Severity switch
        {
            WorkSpellIssueSeverity.Error => "ОШИБКА",
            WorkSpellIssueSeverity.Warning => "ВНИМАНИЕ",
            _ => "СОВЕТ"
        };

        string line = LineNo > 0 ? $"строка {LineNo}: " : string.Empty;
        string fragment = string.IsNullOrWhiteSpace(Fragment) ? string.Empty : $" [{Fragment}]";
        string suggestion = string.IsNullOrWhiteSpace(Suggestion) ? string.Empty : $" Исправить/проверить: {Suggestion}";
        return $"{prefix}: {line}{Message}{fragment}.{suggestion}";
    }
}

public enum WorkSpellIssueSeverity
{
    Info,
    Warning,
    Error
}
