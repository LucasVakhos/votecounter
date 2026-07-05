using VoteCounter.Core.Models;

namespace VoteCounter.Core.Services;

/// <summary>
/// Applies voting rules from Rhyme Machine contest system.
/// </summary>
/// <remarks>
/// Enforces the following rules:
/// - Self-votes (voter voting for own work) become 0
/// - Scores above max become default score
/// - Excess max scores per topic/contest reduced to default
/// - Mode "one max per topic" = limit 1 max score per topic
/// </remarks>
public sealed class VoteRuleService
{
    private static readonly Dictionary<int, string> TopicKeyCache = new();

    /// <summary>
    /// Applies all voting rules to vote blocks in the import result.
    /// </summary>
    /// <param name="contest">The contest with rule definitions.</param>
    /// <param name="result">The import result containing vote blocks to process.</param>
    /// <remarks>
    /// Modifies votes in-place. Removes self-votes, enforces limits per topic, and applies score caps.
    /// </remarks>
    public void Apply(Contest contest, ImportResult result)
    {
        if (contest is null)
            return;

        // Pre-compute work lookup with topic keys
        Dictionary<int, ContestWork> worksByNo = contest.Works
            .Where(x => x.Number > 0)
            .GroupBy(x => x.Number)
            .ToDictionary(g => g.Key, g => g.Last());

        // Pre-compute topic keys
        var topicKeys = new Dictionary<int, string>(worksByNo.Count);
        foreach (var (workNo, work) in worksByNo)
        {
            topicKeys[workNo] = GetTopicKeyInternal(work);
        }

        foreach (ParsedVoteBlock block in result.Blocks)
        {
            ApplyToBlock(contest, worksByNo, block, result.Warnings, topicKeys);
        }
    }

    private static void ApplyToBlock(
        Contest contest,
        IReadOnlyDictionary<int, ContestWork> worksByNo,
        ParsedVoteBlock block,
        IList<string> warnings,
        IReadOnlyDictionary<int, string> topicKeys)
    {
        foreach (VoteEntry entry in block.Votes)
        {
            entry.OriginalScore = entry.Score;
            entry.OriginalScoreText = entry.ScoreText;
            entry.VotedScore = entry.Score;
            entry.VotedScoreText = entry.ScoreText;
            entry.AcceptedScore = entry.Score;
            entry.AcceptedScoreText = entry.ScoreText;

            if (contest.TreatSelfVoteAsZero && worksByNo.TryGetValue(entry.WorkNo, out ContestWork? work))
            {
                if (!string.IsNullOrWhiteSpace(work.Author) && NameNormalizer.Same(block.VoterName, work.Author))
                    ChangeScore(entry, 0m, "0", "самоголосование = 0");
            }

            if (entry.Score > contest.MaxVote)
            {
                decimal newScore = Math.Min(contest.BaseVote, contest.MaxVote);
                ChangeScore(entry, newScore, FormatScore(newScore), $"оценка выше максимальной {contest.MaxVote} -> {FormatScore(newScore)}");
            }

            if (entry.Score == 0m && !contest.AllowZeroVotes && !entry.RuleNote.Contains("самоголос", StringComparison.OrdinalIgnoreCase))
            {
                decimal newScore = Math.Max(1, Math.Min(contest.BaseVote, contest.MaxVote));
                ChangeScore(entry, newScore, FormatScore(newScore), $"0 не разрешён -> {FormatScore(newScore)}");
            }
        }

        ApplyMaxVoteLimits(contest, worksByNo, block, warnings, topicKeys);

        foreach (VoteEntry entry in block.Votes)
        {
            entry.AcceptedScore = entry.Score;
            entry.AcceptedScoreText = entry.ScoreText;
            if (string.IsNullOrWhiteSpace(entry.VotedScoreText))
            {
                entry.VotedScore = entry.OriginalScore;
                entry.VotedScoreText = entry.OriginalScoreText;
            }
        }
    }

    private static void ApplyMaxVoteLimits(
        Contest contest,
        IReadOnlyDictionary<int, ContestWork> worksByNo,
        ParsedVoteBlock block,
        IList<string> warnings,
        IReadOnlyDictionary<int, string> topicKeys)
    {
        int limit = contest.OneMaxVotePerTopic ? 1 : contest.LimitMaxVote;
        if (limit <= 0)
            return;

        IEnumerable<IGrouping<string, VoteEntry>> groups;
        if (contest.LimitMaxVoteByTopic || contest.OneMaxVotePerTopic)
        {
            groups = block.Votes.GroupBy(
                x => topicKeys.TryGetValue(x.WorkNo, out var key) ? key : "Общая тема",
                StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            groups = block.Votes.GroupBy(_ => "__all__", StringComparer.OrdinalIgnoreCase);
        }

        foreach (IGrouping<string, VoteEntry> group in groups)
        {
            List<VoteEntry> maxVotes = group
                .Where(x => x.Score == contest.MaxVote)
                .ToList();

            if (maxVotes.Count <= limit)
                continue;

            List<VoteEntry> extra = maxVotes.Skip(limit).ToList();
            string groupName = group.Key == "__all__" ? "конкурсу" : $"теме '{group.Key}'";
            warnings.Add($"{block.VoterName}: по {groupName} максимальных оценок {maxVotes.Count}, разрешено {limit}. Лишние исправлены.");

            foreach (VoteEntry entry in extra)
            {
                if (contest.DowngradeExtraMaxVoteToBase)
                {
                    decimal newScore = IsPlusMaxVote(entry, contest.MaxVote)
                        ? Math.Min(3.5m, contest.MaxVote)
                        : Math.Min(contest.BaseVote, contest.MaxVote);
                    string newScoreText = IsPlusMaxVote(entry, contest.MaxVote) && newScore == 3.5m ? "3+" : FormatScore(newScore);
                    ChangeScore(entry, newScore, newScoreText, $"лимит {contest.MaxVote} исчерпан: принято {newScoreText}, оригинал сохранён");
                }
                else
                {
                    ChangeScore(entry, 0m, "0", $"лимит {contest.MaxVote} исчерпан: принято 0, оригинал сохранён");
                }
            }
        }
    }

    private static bool IsPlusMaxVote(VoteEntry entry, int maxVote)
    {
        string text = string.IsNullOrWhiteSpace(entry.VotedScoreText) ? entry.OriginalScoreText : entry.VotedScoreText;
        return text.Trim().StartsWith(maxVote.ToString() + "+", StringComparison.Ordinal);
    }

    private static string GetTopicKeyInternal(ContestWork work)
    {
        string topic = (work.Topic ?? string.Empty).Trim();
        return !string.IsNullOrWhiteSpace(topic) ? topic : "Общая тема";
    }

    private static string GetTopicKey(IReadOnlyDictionary<int, ContestWork> worksByNo, int workNo)
    {
        if (worksByNo.TryGetValue(workNo, out ContestWork? work))
        {
            string topic = (work.Topic ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(topic))
                return topic;
        }

        return "Общая тема";
    }

    private static string FormatScore(decimal value)
        => value == decimal.Truncate(value) ? ((int)value).ToString() : value.ToString("0.##");

    private static void ChangeScore(VoteEntry entry, decimal newScore, string newScoreText, string note)
    {
        if (string.IsNullOrWhiteSpace(entry.OriginalScoreText))
        {
            entry.OriginalScore = entry.Score;
            entry.OriginalScoreText = entry.ScoreText;
        }

        if (entry.Score != newScore || !string.Equals(entry.ScoreText, newScoreText, StringComparison.OrdinalIgnoreCase))
            entry.WasChangedByRules = true;

        entry.Score = newScore;
        entry.ScoreText = newScoreText;
        entry.AcceptedScore = newScore;
        entry.AcceptedScoreText = newScoreText;

        if (string.IsNullOrWhiteSpace(entry.RuleNote))
            entry.RuleNote = note;
        else if (!entry.RuleNote.Contains(note, StringComparison.OrdinalIgnoreCase))
            entry.RuleNote += "; " + note;
    }
}
