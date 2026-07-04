using System.Text.RegularExpressions;
using VoteCounter.Models;

namespace VoteCounter.Services;

public sealed class VoteParser
{
    private static readonly Regex TimeRegex = new(
        @"^(вчера|сегодня)?\s*\d{1,2}:\d{2}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SocialDateRegex = new(
        @"^\d{1,2}\s+(?:янв|фев|мар|апр|ма[йя]|июн|июл|авг|сен|сент|окт|ноя|дек)\b.*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public ImportResult Parse(string text, string contestId, Contest? contest = null)
    {
        var result = new ImportResult();
        var current = new ParsedVoteBlock();
        string? candidateVoter = null;
        bool allowZeroVotes = contest?.AllowZeroVotes ?? false;
        string[] lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        for (int index = 0; index < lines.Length; index++)
        {
            int lineNo = index + 1;
            var line = lines[index].Trim();
            var skipKind = GetSkipKind(line);
            if (skipKind != SkipKind.None)
            {
                if (skipKind == SkipKind.SocialBoundary && current.Votes.Count > 0)
                {
                    FinishCurrentBlock(result, current, contest);
                    current = new ParsedVoteBlock();
                }

                // Любая социальная граница ОК/дата/счётчик реакции закрывает старого кандидата.
                // Иначе "Елька )))" из служебных постов может стать автором следующих голосов.
                candidateVoter = null;
                continue;
            }

            if (IsTopicHeaderLine(line, lines, index))
                continue;

            if (IsPictureTitle(line))
                continue;

            if (IsMetaLine(line))
                continue;

            IReadOnlyList<VotePair> pairs = DelphiVoteTextTools.ExtractPairsFromLine(line, allowZeroVotes);
            if (pairs.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(candidateVoter))
                {
                    result.Warnings.Add($"Строка {lineNo}: оценка без имени голосующего пропущена: {line}");
                    continue;
                }

                current.VoterName = candidateVoter.Trim();
                foreach (VotePair pair in pairs)
                {
                    current.Votes.Add(new VoteEntry
                    {
                        ContestId = contestId,
                        VoterName = current.VoterName,
                        VoterKey = NameNormalizer.Normalize(current.VoterName),
                        WorkNo = pair.WorkNo,
                        ScoreText = pair.ScoreText,
                        Score = pair.Score,
                        OriginalScore = pair.Score,
                        OriginalScoreText = pair.ScoreText,
                        VotedScore = pair.Score,
                        VotedScoreText = pair.ScoreText,
                        AcceptedScore = pair.Score,
                        AcceptedScoreText = pair.ScoreText,
                        Comment = pair.Comment,
                        SourceLine = pair.SourceLine,
                        UpdatedAt = DateTime.Now
                    });
                }
                continue;
            }

            if (current.Votes.Count > 0)
            {
                if (LooksLikePossibleVoter(line))
                {
                    FinishCurrentBlock(result, current, contest);
                    current = new ParsedVoteBlock();
                    candidateVoter = line;
                }

                // Комментарии после оценок просто игнорируем до следующего имени голосующего.
                continue;
            }

            if (LooksLikePossibleVoter(line))
                candidateVoter = line;
        }

        if (current.Votes.Count > 0)
            FinishCurrentBlock(result, current, contest);

        ValidateResult(result, contest);
        return result;
    }

    private static void FinishCurrentBlock(ImportResult result, ParsedVoteBlock block, Contest? contest)
    {
        if (block.Votes.Count == 0 || string.IsNullOrWhiteSpace(block.VoterName))
            return;

        var compact = block.Votes
            .GroupBy(x => x.WorkNo)
            .Select(g => g.Last())
            .OrderBy(x => x.WorkNo)
            .ToList();

        int limit = contest?.VoteLimit ?? 0;
        if (limit > 0 && compact.Count > limit)
            compact = compact.Take(limit).ToList();

        block.Votes.Clear();
        block.Votes.AddRange(compact);
        result.Blocks.Add(block);
    }

    private static void ValidateResult(ImportResult result, Contest? contest)
    {
        if (contest is null)
            return;

        HashSet<int> knownWorks = contest.Works
            .Where(x => x.Number > 0)
            .Select(x => x.Number)
            .ToHashSet();

        foreach (ParsedVoteBlock block in result.Blocks)
        {
            if (knownWorks.Count == 0)
                continue;

            List<int> unknown = block.Votes
                .Select(x => x.WorkNo)
                .Where(x => !knownWorks.Contains(x))
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            if (unknown.Count > 0)
                result.Warnings.Add($"{block.VoterName}: номера не найдены в настройках работ: {string.Join(", ", unknown.Select(x => x.ToString("00")))}.");

            List<int> missing = knownWorks
                .Where(x => block.Votes.All(v => v.WorkNo != x))
                .OrderBy(x => x)
                .ToList();

            if (missing.Count > 0)
                result.Warnings.Add($"{block.VoterName}: не указаны оценки для работ: {string.Join(", ", missing.Select(x => x.ToString("00")))}.");
        }
    }

    private static SkipKind GetSkipKind(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return SkipKind.None;

        if (line == ".")
            return SkipKind.None;

        if (line.All(ch => ch == '-' || ch == '—' || ch == '–' || char.IsWhiteSpace(ch)))
            return SkipKind.HardBoundary;

        if (line.Equals("Ответить", StringComparison.OrdinalIgnoreCase))
            return SkipKind.SocialBoundary;

        if (Regex.IsMatch(line, @"^\d+$", RegexOptions.CultureInvariant))
            return SkipKind.SocialBoundary;

        if (SocialDateRegex.IsMatch(line))
            return SkipKind.SocialBoundary;

        if (TimeRegex.IsMatch(line))
            return SkipKind.SocialBoundary;

        return SkipKind.None;
    }

    private static bool IsTopicHeaderLine(string line, IReadOnlyList<string> lines, int index)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        if (line.StartsWith("Художник", StringComparison.OrdinalIgnoreCase))
            return true;

        string next = FindNextUsefulLine(lines, index + 1);
        if (IsPictureTitle(next))
            return true;

        return false;
    }

    private static string FindNextUsefulLine(IReadOnlyList<string> lines, int startIndex)
    {
        for (int i = startIndex; i < lines.Count; i++)
        {
            string value = (lines[i] ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value) || value == ".")
                continue;

            return value;
        }

        return string.Empty;
    }

    private static bool IsPictureTitle(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        string trimmed = line.Trim();
        if (trimmed.StartsWith('"') || trimmed.StartsWith('“') || trimmed.StartsWith('«'))
            return true;

        return false;
    }

    private static bool IsMetaLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return true;

        if (line.Equals("Новый шаблон для голосования", StringComparison.OrdinalIgnoreCase))
            return true;

        if (line.StartsWith("Шаблон для голосования", StringComparison.OrdinalIgnoreCase))
            return true;

        if (line.StartsWith("Ответила ", StringComparison.OrdinalIgnoreCase))
            return true;

        if (line.StartsWith("Ответил ", StringComparison.OrdinalIgnoreCase))
            return true;

        var lower = line.ToLowerInvariant();
        if (lower.StartsWith("моё личное") || lower.StartsWith("мое личное"))
            return true;

        if (lower.StartsWith("по “") || lower.StartsWith("по \"") || lower.StartsWith("по «"))
            return true;

        return false;
    }

    private static bool LooksLikePossibleVoter(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        if (IsPictureTitle(line) || IsMetaLine(line))
            return false;

        if (line.StartsWith("№", StringComparison.OrdinalIgnoreCase))
            return false;

        if (line.Contains(" -") || line.Contains("- ") || line.Contains('—') || line.Contains('–'))
            return false;

        if (line.Contains('"') || line.Contains('«') || line.Contains('»') || line.Contains('“') || line.Contains('”'))
            return false;

        if (line.EndsWith(':'))
            return false;

        if (line.Length > 80)
            return false;

        if (line.Any(ch => ch is ',' or '.' or ';' or '!' or '?' or '…'))
            return false;

        int letterCount = line.Count(char.IsLetter);
        if (letterCount < 2)
            return false;

        int wordCount = line
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(x => x.Any(char.IsLetter));

        return wordCount is >= 1 and <= 6;
    }

    private enum SkipKind
    {
        None,
        HardBoundary,
        SocialBoundary
    }
}
