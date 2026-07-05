namespace Rhymers.Core.Models;

/// <summary>
/// Отчёт о результатах импорта из старой Firebird-базы.
/// </summary>
public sealed class FirebirdImportReport
{
    public List<Contest> Contests { get; } = new();
    public Dictionary<string, List<VoteEntry>> VotesByContestId { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Warnings { get; } = new();
    public string ConnectionMode { get; set; } = string.Empty;
    public int WorkCount => Contests.Sum(x => x.Works.Count);
    public int VoterCount => Contests.Sum(x => x.Voters.Count);
    public int VoteCount => VotesByContestId.Values.Sum(x => x.Count);
}
