using System.Text;
using System.Text.RegularExpressions;
using VoteCounter.Core.Models;

namespace VoteCounter.Core.Services;

/// <summary>
/// Перенос рабочей логики из Delphi-форм fr_2_vote_step/f_vote*.pas:
/// - не привязывается к красивому шаблону;
/// - понимает разные тире;
/// - вытаскивает пары №-оценка из грязной строки;
/// - оставляет последнюю оценку при повторе одного номера внутри блока.
/// </summary>
public static class DelphiVoteTextTools
{
    private static readonly Regex PairRegex = new(
        @"(?<!\d)(?<no>\d{1,3})\s*[-–—\u00AD]\s*(?<score>[0-4](?:\s*[+\-−])?)(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<VotePair> ExtractPairsFromLine(string? line, bool allowZeroVotes)
    {
        string source = line ?? string.Empty;
        var matches = PairRegex.Matches(source);
        if (matches.Count == 0)
            return Array.Empty<VotePair>();

        var result = new List<VotePair>();
        for (int i = 0; i < matches.Count; i++)
        {
            Match match = matches[i];
            int workNo = int.Parse(match.Groups["no"].Value);
            string scoreText = match.Groups["score"].Value.Replace(" ", string.Empty).Replace('−', '-');
            decimal score = NormalizeScore(scoreText);
            if (score == 0m && !allowZeroVotes)
                continue;

            int commentStart = match.Index + match.Length;
            int commentEnd = i + 1 < matches.Count ? matches[i + 1].Index : source.Length;
            string comment = NormalizeComment(source[commentStart..commentEnd]);

            result.Add(new VotePair
            {
                WorkNo = workNo,
                Score = score,
                ScoreText = scoreText,
                Comment = comment,
                SourceLine = source.Trim()
            });
        }

        return result;
    }

    public static List<VotePair> CompactLastVoteByWork(IEnumerable<VotePair> pairs)
    {
        return pairs
            .GroupBy(x => x.WorkNo)
            .Select(x => x.Last())
            .OrderBy(x => x.WorkNo)
            .ToList();
    }

    public static string CreateCanonicalVoteList(IEnumerable<VoteEntry> votes, bool allowZeroVotes)
    {
        return string.Join("," + Environment.NewLine,
            votes
                .Where(x => allowZeroVotes || x.Score > 0)
                .OrderBy(x => x.WorkNo)
                .Select(x => $"{x.WorkNo:00}-{x.ScoreText}"));
    }

    /// <summary>
    /// Аналог старого PrepareDataToPost: оставляет только цифры и тире, всё прочее превращает в разделители.
    /// В UI пока не вызывается автоматически, чтобы не уничтожать имена голосующих при вставке всей ленты.
    /// </summary>
    /// <summary>
    /// Нормализация оценок конкурса.
    /// 3+ теперь считается как 3.5; остальные +/- оставляем как исходный целый балл.
    /// </summary>
    public static decimal NormalizeScore(string scoreText)
    {
        string text = (scoreText ?? string.Empty).Trim().Replace(" ", string.Empty).Replace('−', '-');
        if (text.StartsWith("3+", StringComparison.Ordinal))
            return 3.5m;

        if (text.StartsWith("3-", StringComparison.Ordinal))
            return 2.5m;

        if (text.Length == 0 || !char.IsDigit(text[0]))
            return 0m;

        return text[0] - '0';
    }

    public static string NormalizeRawVoteTextLikeDelphi(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (char ch in text)
        {
            if (char.IsDigit(ch))
                sb.Append(ch);
            else if (IsDash(ch))
                sb.Append('-');
            else
                sb.Append(' ');
        }

        string value = sb.ToString();
        value = Regex.Replace(value, @"-\s+", "-");
        value = Regex.Replace(value, @"\s+-", "-");
        value = Regex.Replace(value, @"\s+", ",").Trim(',');
        value = Regex.Replace(value, @",{2,}", ",");
        return value;
    }

    /// <summary>
    /// Аналог Revert из Delphi-проверки: меняет стороны у строк вида A-B на B-A.
    /// Полезно, если кто-то прислал список в обратном формате.
    /// </summary>
    public static string ReversePairsByDivider(string? text, string divider = "-")
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrEmpty(divider))
            return text ?? string.Empty;

        var lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            int pos = line.IndexOf(divider, StringComparison.Ordinal);
            if (pos <= 0)
                continue;

            string left = line[..pos].Trim();
            string right = line[(pos + divider.Length)..].Trim();
            if (left.Length == 0 || right.Length == 0)
                continue;

            lines[i] = right + "-" + left;
        }

        return string.Join(Environment.NewLine, lines.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static bool IsDash(char ch)
        => ch is '-' or '–' or '—' or '\u00AD';

    private static string NormalizeComment(string value)
    {
        value = value.Trim();
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        value = value.Trim(',', ';', '.', ' ');
        if (value.StartsWith('(') && value.EndsWith(')') && value.Length > 2)
            value = value[1..^1].Trim();

        return value;
    }
}
