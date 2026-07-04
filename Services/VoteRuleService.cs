using VoteCounter.Models;

namespace VoteCounter.Services;

/// <summary>
/// Применяет правила голосования из Rhyme Machine без внешней БД:
/// - голос за свою работу превращает в 0;
/// - оценка выше максимальной превращается в базовую;
/// - лишние максимальные оценки по конкурсу/теме превращаются в базовую;
/// - режим "одна максимальная в теме" = лимит 1 внутри каждой темы.
/// </summary>
public sealed class VoteRuleService
{
    public void Apply(Contest contest, ImportResult result)
    {
        if (contest is null)
            return;

        Dictionary<int, ContestWork> worksByNo = contest.Works
            .Where(x => x.Number > 0)
            .GroupBy(x => x.Number)
            .ToDictionary(g => g.Key, g => g.Last());

        foreach (ParsedVoteBlock block in result.Blocks)
        {
            ApplyToBlock(contest, worksByNo, block, result.Warnings);
        }
    }

    private static void ApplyToBlock(
        Contest contest,
        IReadOnlyDictionary<int, ContestWork> worksByNo,
        ParsedVoteBlock block,
        IList<string> warnings)
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

        ApplyMaxVoteLimits(contest, worksByNo, block, warnings);

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
        IList<string> warnings)
    {
        int limit = contest.OneMaxVotePerTopic ? 1 : contest.LimitMaxVote;
        if (limit <= 0)
            return;

        IEnumerable<IGrouping<string, VoteEntry>> groups;
        if (contest.LimitMaxVoteByTopic || contest.OneMaxVotePerTopic)
        {
            groups = block.Votes.GroupBy(x => GetTopicKey(worksByNo, x.WorkNo), StringComparer.OrdinalIgnoreCase);
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
