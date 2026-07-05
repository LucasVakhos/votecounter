using System.Text.RegularExpressions;
using Rhymers.Core.Models;

namespace Rhymers.Core.Services;

/// <summary>
/// Импорт раскрытия авторства после конкурса.
/// Поддерживает формат:
/// 001, 004, 008 - Лидия Андрианова (Медведева)
/// 002 - Марина Аллахвердова
/// </summary>
public sealed class AuthorDisclosureImporter
{
    private static readonly Regex SummaryRegex = new(
        @"участи[ея]\s+(?<authors>\d+)\s+автор(?:ов|а)?\s*\(\s*(?<works>\d+)\s+стих",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex MappingLineRegex = new(
        @"^\s*(?<numbers>(?:№?\s*\d{1,3}\s*(?:[,;]\s*)?)+)\s*[-–—]\s*(?<author>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex NumberRegex = new(
        @"\d{1,3}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public AuthorDisclosureResult Parse(string? text)
    {
        var result = new AuthorDisclosureResult();
        foreach (string rawLine in SplitLines(text))
        {
            string line = rawLine.Trim();
            if (ShouldSkip(line))
                continue;

            Match summary = SummaryRegex.Match(line);
            if (summary.Success)
            {
                result.ExpectedAuthorCount = ParseInt(summary.Groups["authors"].Value);
                result.ExpectedWorkCount = ParseInt(summary.Groups["works"].Value);
                continue;
            }

            Match match = MappingLineRegex.Match(line);
            if (!match.Success)
                continue;

            string author = CleanAuthor(match.Groups["author"].Value);
            if (string.IsNullOrWhiteSpace(author))
            {
                result.EmptyAuthorLines++;
                result.Warnings.Add("Пропущена строка без автора: " + line);
                continue;
            }

            List<int> numbers = NumberRegex.Matches(match.Groups["numbers"].Value)
                .Select(x => ParseInt(x.Value))
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (numbers.Count == 0)
            {
                result.Warnings.Add("Пропущена строка без номеров работ: " + line);
                continue;
            }

            foreach (int number in numbers)
            {
                if (result.AuthorsByWorkNo.ContainsKey(number))
                {
                    result.DuplicateWorkNumbers++;
                    result.Warnings.Add($"№{number:000}: автор указан повторно, оставлен последний вариант: {author}.");
                }

                result.AuthorsByWorkNo[number] = author;
            }
        }

        if (result.ExpectedWorkCount > 0 && result.ExpectedWorkCount != result.WorkCount)
            result.Warnings.Add($"В шапке заявлено работ: {result.ExpectedWorkCount}, в раскрытии распознано: {result.WorkCount}.");

        if (result.ExpectedAuthorCount > 0 && result.ExpectedAuthorCount != result.AuthorCount)
            result.Warnings.Add($"В шапке заявлено авторов: {result.ExpectedAuthorCount}, в раскрытии распознано: {result.AuthorCount}.");

        return result;
    }

    private static IEnumerable<string> SplitLines(string? text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x));
    }

    private static bool ShouldSkip(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line == ".")
            return true;

        if (line.All(ch => ch == '-' || ch == '–' || ch == '—' || char.IsWhiteSpace(ch)))
            return true;

        return false;
    }

    private static string CleanAuthor(string value)
    {
        value = (value ?? string.Empty).Trim();
        value = value.Trim('.', ',', ';', ':');
        value = Regex.Replace(value, @"\s+", " ").Trim();
        return value;
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(value, out int result) ? result : 0;
    }
}
