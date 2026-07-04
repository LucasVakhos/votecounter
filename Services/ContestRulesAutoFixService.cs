using System.Text.RegularExpressions;
using VoteCounter.Models;

namespace VoteCounter.Services;

/// <summary>
/// Автоматически восстанавливает правила конкурса из текста голосования,
/// если конкурс создан из текста/старой базы и правила в базе ещё не заполнены.
/// </summary>
public sealed class ContestRulesAutoFixService
{
    private static readonly Regex AllowedScoresRegex = new(
        @"(?:оценками|оценки|баллами)\s*[:\-]?\s*(?<scores>[0-9,\.\s]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public bool EnsureRules(Contest contest, string? sourceText, IList<string>? warnings = null)
    {
        if (contest is null)
            return false;

        string text = sourceText ?? string.Empty;
        string lower = text.ToLowerInvariant();
        bool changed = false;

        if (contest.BaseVote <= 0)
        {
            contest.BaseVote = 3;
            changed = true;
        }

        if (contest.MaxVote <= 0)
        {
            contest.MaxVote = 4;
            changed = true;
        }

        Match allowed = AllowedScoresRegex.Match(text);
        if (allowed.Success)
        {
            List<int> scores = Regex.Matches(allowed.Groups["scores"].Value, @"\d+")
                .Select(x => int.TryParse(x.Value, out int n) ? n : -1)
                .Where(x => x >= 0)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            List<int> positiveScores = scores.Where(x => x > 0).ToList();
            if (positiveScores.Count > 0)
            {
                int max = positiveScores.Max();
                int baseVote = positiveScores.Contains(3) ? 3 : positiveScores[Math.Max(0, positiveScores.Count - 2)];
                if (contest.MaxVote != max)
                {
                    contest.MaxVote = max;
                    changed = true;
                }

                if (contest.BaseVote != baseVote)
                {
                    contest.BaseVote = baseVote;
                    changed = true;
                }
                bool allowZeroFromText = scores.Contains(0);
                if (contest.AllowZeroVotes != allowZeroFromText)
                {
                    contest.AllowZeroVotes = allowZeroFromText;
                    changed = true;
                }
            }
        }

        bool oneMaxPerTopicDetected =
            lower.Contains("высший балл") &&
            (lower.Contains("только одной работе") || lower.Contains("одной работе")) &&
            (lower.Contains("одном задании") || lower.Contains("в одном задании") || lower.Contains("задании"));

        if (oneMaxPerTopicDetected)
        {
            if (!contest.OneMaxVotePerTopic)
            {
                contest.OneMaxVotePerTopic = true;
                changed = true;
            }

            if (!contest.LimitMaxVoteByTopic)
            {
                contest.LimitMaxVoteByTopic = true;
                changed = true;
            }

            if (contest.LimitMaxVote != 1)
            {
                contest.LimitMaxVote = 1;
                changed = true;
            }

            if (!contest.DowngradeExtraMaxVoteToBase)
            {
                contest.DowngradeExtraMaxVoteToBase = true;
                changed = true;
            }
        }

        if (lower.Contains("голосуем за 100%") || lower.Contains("голосовать за каждую работу"))
        {
            // Количество работ не жёстко ограничиваем VoteLimit, потому что это поле режет лишние оценки.
            // Контроль пропущенных работ уже делает VoteParser.ValidateResult по списку Works.
        }

        if (lower.Contains("за свои работы") || lower.Contains("за свою работу") || lower.Contains("самоголос"))
        {
            if (!contest.TreatSelfVoteAsZero)
            {
                contest.TreatSelfVoteAsZero = true;
                changed = true;
            }
        }

        if (contest.MaxVote < contest.BaseVote)
        {
            contest.BaseVote = contest.MaxVote;
            changed = true;
        }

        if (changed)
        {
            contest.UpdatedAt = DateTime.Now;
            warnings?.Add("Правила конкурса автоматически восстановлены из текста голосования: 1/2/3/4, 3+ = 3.5, одна 4 в теме, лишняя 4 -> базовая оценка, самоголос = 0.");
        }

        return changed;
    }
}
