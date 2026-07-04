using System.Text.RegularExpressions;
using VoteCounter.Models;

namespace VoteCounter.Services;

public sealed class PrivateMessageWorkImportResult
{
    public List<ContestWork> Works { get; } = new();
    public List<string> Warnings { get; } = new();
    public int RejectedBlocks { get; set; }

    public int Count => Works.Count;
}

/// <summary>
/// Разбор работ, которые ведущий получает личными сообщениями.
/// Поддерживает блоки "Автор: ... / Название: ..." и короткий формат:
/// автор, название, затем текст работы.
/// </summary>
public sealed class PrivateMessageWorkImporter
{
    private static readonly Regex AuthorLineRegex = new(
        @"^\s*(?:автор|author)\s*[:\-–—]\s*(?<value>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TitleLineRegex = new(
        @"^\s*(?:название|заголовок|title)\s*[:\-–—]\s*(?<value>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SeparatorLineRegex = new(
        @"^\s*(?:[-–—=_*]\s*){3,}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly SingleWorkSubmissionImporter _singleWorkImporter = new();

    public PrivateMessageWorkImportResult Parse(string? text, int firstNumber)
    {
        var result = new PrivateMessageWorkImportResult();
        var blocks = SplitBlocks(text).ToList();
        int number = Math.Max(1, firstNumber);

        for (int i = 0; i < blocks.Count; i++)
        {
            if (!TryParseBlock(blocks[i], number, out ContestWork? work, out string warning))
            {
                result.RejectedBlocks++;
                result.Warnings.Add($"Блок {i + 1}: {warning}");
                continue;
            }

            result.Works.Add(work!);
            number++;
        }

        return result;
    }

    private bool TryParseBlock(List<string> block, int number, out ContestWork? work, out string warning)
    {
        work = null;
        warning = string.Empty;

        var lines = block.Select(x => x.TrimEnd()).Where(x => !IsNoiseLine(x)).ToList();
        if (lines.Count == 0)
        {
            warning = "пустой блок";
            return false;
        }

        string author = string.Empty;
        string title = string.Empty;
        var contentLines = new List<string>();
        int firstContentIndex = 0;

        for (int i = 0; i < lines.Count; i++)
        {
            Match authorMatch = AuthorLineRegex.Match(lines[i]);
            if (authorMatch.Success)
            {
                author = Clean(authorMatch.Groups["value"].Value);
                firstContentIndex = i + 1;
                continue;
            }

            Match titleMatch = TitleLineRegex.Match(lines[i]);
            if (titleMatch.Success)
            {
                title = CleanTitle(titleMatch.Groups["value"].Value);
                firstContentIndex = i + 1;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(author) || !string.IsNullOrWhiteSpace(title))
                contentLines.Add(lines[i]);
        }

        if (string.IsNullOrWhiteSpace(author) && string.IsNullOrWhiteSpace(title))
        {
            var meaningful = lines.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (meaningful.Count < 3)
            {
                warning = "нужно минимум три строки: автор, название, текст";
                return false;
            }

            author = Clean(meaningful[0]);
            title = CleanTitle(meaningful[1]);
            contentLines = meaningful.Skip(2).ToList();
        }
        else
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                string? firstContent = contentLines.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                if (!string.IsNullOrWhiteSpace(firstContent))
                {
                    title = CleanTitle(firstContent);
                    contentLines.Remove(firstContent);
                }
            }

            if (contentLines.Count == 0 && firstContentIndex < lines.Count)
                contentLines = lines.Skip(firstContentIndex).Where(x => !AuthorLineRegex.IsMatch(x) && !TitleLineRegex.IsMatch(x)).ToList();
        }

        if (string.IsNullOrWhiteSpace(author))
        {
            warning = "не найден автор";
            return false;
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            warning = "не найдено название";
            return false;
        }

        if (!contentLines.Any(x => !string.IsNullOrWhiteSpace(x)))
        {
            warning = "не найден текст работы";
            return false;
        }

        string content = BuildContent(title, contentLines);
        SingleWorkSubmission submission = _singleWorkImporter.Parse(content, author, number);
        work = _singleWorkImporter.ToContestWork(submission);
        return true;
    }

    private static IEnumerable<List<string>> SplitBlocks(string? text)
    {
        var lines = (text ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .ToList();

        var current = new List<string>();
        bool hasExplicitBlock = lines.Any(x => AuthorLineRegex.IsMatch(x));

        foreach (string rawLine in lines)
        {
            string line = rawLine.TrimEnd();
            bool separator = SeparatorLineRegex.IsMatch(line);
            bool startsNewExplicit = hasExplicitBlock && AuthorLineRegex.IsMatch(line) && current.Any(x => !string.IsNullOrWhiteSpace(x));

            if (separator || startsNewExplicit)
            {
                if (current.Any(x => !string.IsNullOrWhiteSpace(x)))
                    yield return current;
                current = new List<string>();
            }

            if (!separator)
                current.Add(rawLine);
        }

        if (current.Any(x => !string.IsNullOrWhiteSpace(x)))
            yield return current;
    }

    private static string BuildContent(string title, List<string> contentLines)
    {
        var body = contentLines
            .SkipWhile(string.IsNullOrWhiteSpace)
            .ToList();

        if (body.Count > 0 && CleanTitle(body[0]).Equals(title, StringComparison.OrdinalIgnoreCase))
            return string.Join(Environment.NewLine, body).Trim();

        return (title + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, body)).Trim();
    }

    private static bool IsNoiseLine(string line)
    {
        string clean = line.Trim();
        return clean.Equals("Ответить", StringComparison.OrdinalIgnoreCase)
            || clean.Equals("Пересланное сообщение", StringComparison.OrdinalIgnoreCase);
    }

    private static string Clean(string? value)
    {
        return Regex.Replace((value ?? string.Empty).Trim(), @"\s+", " ").Trim();
    }

    private static string CleanTitle(string? value)
    {
        string clean = Clean(value);
        return clean.Trim('"', '«', '»', '“', '”', '„');
    }
}
