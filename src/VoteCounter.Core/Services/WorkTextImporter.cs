using System.Text.RegularExpressions;
using VoteCounter.Core.Models;

namespace VoteCounter.Core.Services;

public enum WorkTextImportMode
{
    /// <summary>
    /// До закрытия конкурса авторы работ неизвестны: любая найденная работа получает автора "Неизвестный автор".
    /// </summary>
    WorksBeforeClose,

    /// <summary>
    /// Таблицу ведёт ведущий: авторы известны, поэтому первичный импорт может сразу брать автора из строки.
    /// </summary>
    WorksWithKnownAuthors,

    /// <summary>
    /// После закрытия конкурса: список "номер - работа - автор" или "номер - автор" применяет авторство к существующим работам.
    /// </summary>
    ApplyAuthorsAfterClose
}

public sealed class WorkTextImportResult
{
    public List<ContestWork> Works { get; } = new();
    public List<string> Warnings { get; } = new();
    public int DuplicateNumbers { get; set; }
    public int AuthorsDetected { get; set; }

    public int Count => Works.Count;
}

/// <summary>
/// Быстрый импорт работ и последующее раскрытие авторства.
/// Если таблицу ведёт не ведущий, первичный импорт ставит автора "Неизвестный автор".
/// Если таблицу ведёт ведущий, первичный импорт может сразу брать автора из строки.
/// После закрытия включается отдельный режим ApplyAuthorsAfterClose для применения списка авторства.
/// </summary>
public sealed class WorkTextImporter
{
    public const string UnknownAuthor = "Неизвестный автор";

    private static readonly Regex NumberedLineRegex = new(
        @"^\s*(?:№\s*)?(?<no>\d{1,3})(?:\s*[\.)\:]\s*|\s*[-–—]\s*|\s+)(?<tail>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex NumberOnlyRegex = new(
        @"^\s*(?:№\s*)?(?<no>\d{1,3})\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex VoteLikeTailRegex = new(
        @"^[0-4](?:\s*[+\-−])?(?:\s*\(.*\))?\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OkDateLineRegex = new(
        @"^\d{1,2}\s+[а-яё]{3,}(?:\s*,.*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex QuotedTitleLineRegex = new(
        @"[""«“„](?<title>[^""»”]{2,140})[""»”]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public WorkTextImportResult Parse(string? text)
    {
        return Parse(text, WorkTextImportMode.WorksBeforeClose);
    }

    public WorkTextImportResult Parse(string? text, WorkTextImportMode mode)
    {
        var result = new WorkTextImportResult();
        var byNumber = new Dictionary<int, ContestWork>();
        var lines = SplitLines(text).ToList();
        string currentTopic = string.Empty;
        string currentTopicSourceAuthor = string.Empty;

        // ОК/форумный экспорт имеет служебные строки после каждой работы:
        // "Ответить", даты, счётчики реакций 1/2/3/4, имя ведущего.
        // Если включать общий парсер, эти счётчики могут быть приняты за номера работ
        // и затереть реальные 002/003/004. Поэтому для такого текста используем
        // строгий режим: берём только блоки вида 001 / Художник / Автор картины / "Картина".
        if (LooksLikeOkContestExport(lines) && mode != WorkTextImportMode.ApplyAuthorsAfterClose)
        {
            for (int i = 0; i < lines.Count; i++)
                TryReadOkWorkBlock(lines, ref i, byNumber, result, mode);

            result.Works.AddRange(byNumber.Values.OrderBy(x => x.Number).ThenBy(x => x.Title));
            return result;
        }

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            if (ShouldSkip(line))
                continue;

            if (TryReadTopicTitle(line, out string topicTitle))
            {
                currentTopic = topicTitle;
                currentTopicSourceAuthor = ReadTopicAuthor(lines, ref i);
                continue;
            }

            if (TryReadOkWorkBlock(lines, ref i, byNumber, result, mode))
                continue;

            if (TryReadNumberOnlyBlock(lines, ref i, currentTopic, byNumber, result, mode))
                continue;

            Match match = NumberedLineRegex.Match(line);
            if (!match.Success)
                continue;

            int number = int.Parse(match.Groups["no"].Value);
            string tail = match.Groups["tail"].Value.Trim();
            if (number <= 0)
                continue;

            // Строки вида 01 - 3 внутри шаблона голосования означают только номер работы.
            if (IsVoteLikeTail(tail))
            {
                if (mode == WorkTextImportMode.ApplyAuthorsAfterClose)
                    continue;

                Upsert(byNumber, result, new ContestWork
                {
                    Number = number,
                    Title = currentTopic,
                    Author = UnknownAuthor,
                    Topic = BuildTopic(currentTopic, currentTopicSourceAuthor)
                });
                continue;
            }

            ContestWork work = ParseNumberedWorkLine(number, tail, currentTopic, mode);
            Upsert(byNumber, result, work);
        }

        result.Works.AddRange(byNumber.Values.OrderBy(x => x.Number).ThenBy(x => x.Title));
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

        if (line.Equals("Ответить", StringComparison.OrdinalIgnoreCase))
            return true;

        if (line.Equals("Новый шаблон для голосования", StringComparison.OrdinalIgnoreCase))
            return true;

        if (line == "1" || line == "0")
            return true;

        string lower = line.ToLowerInvariant();
        if (lower.StartsWith("вчера ") || lower.StartsWith("сегодня "))
            return true;

        if (Regex.IsMatch(line, @"^\d{1,2}:\d{2}$"))
            return true;

        if (OkDateLineRegex.IsMatch(line))
            return true;

        if (line.All(ch => ch == '-' || ch == '–' || ch == '—' || char.IsWhiteSpace(ch)))
            return true;

        return false;
    }

    private static bool TryReadTopicTitle(string line, out string title)
    {
        title = string.Empty;
        string trimmed = line.Trim();
        if (trimmed.Length < 2)
            return false;

        char first = trimmed[0];
        if (first is not ('"' or '«' or '“'))
            return false;

        title = CleanWorkText(trimmed);
        title = title.TrimEnd('-').Trim();
        return !string.IsNullOrWhiteSpace(title);
    }

    private static string ReadTopicAuthor(List<string> lines, ref int index)
    {
        int next = index + 1;
        while (next < lines.Count && ShouldSkip(lines[next]))
            next++;

        if (next >= lines.Count)
            return string.Empty;

        string candidate = lines[next].Trim();
        if (NumberedLineRegex.IsMatch(candidate) || TryReadTopicTitle(candidate, out _))
            return string.Empty;

        if (candidate.Length > 100)
            return string.Empty;

        index = next;
        return CleanWorkText(candidate);
    }

    private static bool LooksLikeOkContestExport(List<string> lines)
    {
        int okBlocks = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            Match numberMatch = NumberOnlyRegex.Match(lines[i]);
            if (!numberMatch.Success)
                continue;

            string rawNumber = numberMatch.Groups["no"].Value;
            string rawLine = lines[i].Trim();
            if (rawNumber.Length < 2 && !rawLine.StartsWith("№", StringComparison.OrdinalIgnoreCase))
                continue;

            string marker = ReadNextMeaningfulLine(lines, i + 1, out _);
            if (IsOkArtistMarker(marker))
                okBlocks++;
        }

        return okBlocks > 0;
    }

    private static bool TryReadOkWorkBlock(
        List<string> lines,
        ref int index,
        Dictionary<int, ContestWork> byNumber,
        WorkTextImportResult result,
        WorkTextImportMode mode)
    {
        if (mode == WorkTextImportMode.ApplyAuthorsAfterClose)
            return false;

        Match numberMatch = NumberOnlyRegex.Match(lines[index]);
        if (!numberMatch.Success)
            return false;

        string rawNumber = numberMatch.Groups["no"].Value;
        string rawLine = lines[index].Trim();
        if (rawNumber.Length < 2 && !rawLine.StartsWith("№", StringComparison.OrdinalIgnoreCase))
            return false;

        int number = int.Parse(rawNumber);
        if (number <= 0)
            return false;

        string artistMarker = ReadNextMeaningfulLine(lines, index + 1, out int markerIndex);
        if (!IsOkArtistMarker(artistMarker))
            return false;

        string sourceAuthor = string.Empty;
        int searchFrom = markerIndex + 1;
        if (artistMarker.Equals("Художник", StringComparison.OrdinalIgnoreCase))
        {
            sourceAuthor = ReadNextMeaningfulLine(lines, searchFrom, out int authorIndex);
            if (string.IsNullOrWhiteSpace(sourceAuthor) || NumberOnlyRegex.IsMatch(sourceAuthor) || IsOkNoiseLine(sourceAuthor))
                return false;

            searchFrom = authorIndex + 1;
        }
        else
        {
            sourceAuthor = CleanWorkText(artistMarker);
        }

        string title = string.Empty;
        int titleIndex = -1;
        for (int i = searchFrom; i < Math.Min(lines.Count, searchFrom + 8); i++)
        {
            string candidate = lines[i].Trim();
            if (ShouldSkip(candidate) || IsOkNoiseLine(candidate))
                continue;

            if (TryExtractQuotedTitle(candidate, out title))
            {
                titleIndex = i;
                break;
            }

            if (NumberOnlyRegex.IsMatch(candidate))
                return false;
        }

        if (titleIndex < 0 || string.IsNullOrWhiteSpace(title))
            return false;

        var contentLines = new List<string>();
        int stopIndex = titleIndex + 1;
        for (int i = titleIndex + 1; i < lines.Count; i++)
        {
            string candidate = lines[i].Trim();
            if (IsOkDateLine(candidate) || candidate.Equals("Ответить", StringComparison.OrdinalIgnoreCase))
            {
                stopIndex = i;
                break;
            }

            if (IsLikelyOkWorkStart(lines, i))
            {
                stopIndex = i;
                break;
            }

            stopIndex = i + 1;
            if (string.IsNullOrWhiteSpace(candidate) || IsOkMediaMarker(candidate))
                continue;

            contentLines.Add(candidate);
        }

        string topic = BuildTopic(title, sourceAuthor);
        Upsert(byNumber, result, new ContestWork
        {
            Number = number,
            Title = title,
            Author = UnknownAuthor,
            Topic = topic,
            Content = string.Join(Environment.NewLine, contentLines).Trim()
        });

        index = Math.Max(index, stopIndex - 1);
        return true;
    }

    private static bool IsLikelyOkWorkStart(List<string> lines, int index)
    {
        if (!NumberOnlyRegex.IsMatch(lines[index]))
            return false;

        string next = ReadNextMeaningfulLine(lines, index + 1, out _);
        return IsOkArtistMarker(next);
    }

    private static bool IsOkArtistMarker(string line)
    {
        string clean = CleanWorkText(line);
        return clean.Equals("Художник", StringComparison.OrdinalIgnoreCase) ||
               clean.StartsWith("Художник неизвест", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryExtractQuotedTitle(string line, out string title)
    {
        title = string.Empty;
        Match match = QuotedTitleLineRegex.Match(line);
        if (!match.Success)
            return false;

        title = CleanWorkText(match.Groups["title"].Value);
        return !string.IsNullOrWhiteSpace(title);
    }

    private static bool IsOkDateLine(string line)
    {
        return OkDateLineRegex.IsMatch((line ?? string.Empty).Trim());
    }

    private static bool IsOkNoiseLine(string line)
    {
        string clean = CleanWorkText(line);
        return clean.Equals("Ответить", StringComparison.OrdinalIgnoreCase) ||
               clean.Equals("Елька )))", StringComparison.OrdinalIgnoreCase) ||
               IsOkDateLine(clean) ||
               Regex.IsMatch(clean, @"^\d{1,2}:\d{2}$");
    }

    private static bool IsOkMediaMarker(string line)
    {
        string clean = CleanWorkText(line);
        return clean.Equals(".", StringComparison.Ordinal) ||
               clean.Equals(":", StringComparison.Ordinal) ||
               Regex.IsMatch(clean, @"^[,;:]+$", RegexOptions.CultureInvariant) ||
               Regex.IsMatch(clean, @"^\(\s*картина\s*\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool TryReadNumberOnlyBlock(
        List<string> lines,
        ref int index,
        string currentTopic,
        Dictionary<int, ContestWork> byNumber,
        WorkTextImportResult result,
        WorkTextImportMode mode)
    {
        Match numberMatch = NumberOnlyRegex.Match(lines[index]);
        if (!numberMatch.Success)
            return false;

        int number = int.Parse(numberMatch.Groups["no"].Value);
        if (number <= 0)
            return false;

        string first = ReadNextMeaningfulLine(lines, index + 1, out int firstIndex);
        if (string.IsNullOrWhiteSpace(first) || NumberedLineRegex.IsMatch(first))
            return false;

        string second = ReadNextMeaningfulLine(lines, firstIndex + 1, out int secondIndex);
        bool hasSecond = !string.IsNullOrWhiteSpace(second) && !NumberedLineRegex.IsMatch(second) && !TryReadTopicTitle(second, out _);

        string title;
        string author;
        if (mode == WorkTextImportMode.ApplyAuthorsAfterClose)
        {
            // После закрытия поддерживаем блок:
            // 04 / Название работы / Автор
            title = CleanWorkText(first);
            author = hasSecond ? CleanWorkText(second) : CleanWorkText(first);
            if (hasSecond)
                index = secondIndex;
            else
                index = firstIndex;
        }
        else if (mode == WorkTextImportMode.WorksWithKnownAuthors)
        {
            // Режим ведущего: если автор указан следующей строкой, берём его сразу.
            title = CleanWorkText(first);
            author = hasSecond ? CleanWorkText(second) : UnknownAuthor;
            if (hasSecond)
                index = secondIndex;
            else
                index = firstIndex;
        }
        else
        {
            // Сторонний счётчик до раскрытия: автор скрыт.
            title = CleanWorkText(first);
            author = UnknownAuthor;
            if (hasSecond)
                index = secondIndex;
            else
                index = firstIndex;
        }

        Upsert(byNumber, result, new ContestWork
        {
            Number = number,
            Title = title,
            Author = author,
            Topic = currentTopic
        });
        return true;
    }

    private static string ReadNextMeaningfulLine(List<string> lines, int startIndex, out int foundIndex)
    {
        foundIndex = startIndex;
        for (int i = startIndex; i < lines.Count; i++)
        {
            if (ShouldSkip(lines[i]))
                continue;

            foundIndex = i;
            return lines[i].Trim();
        }

        return string.Empty;
    }

    private static ContestWork ParseNumberedWorkLine(int number, string tail, string currentTopic, WorkTextImportMode mode)
    {
        string clean = CleanWorkText(tail);
        string title = clean;
        string author = mode == WorkTextImportMode.WorksBeforeClose ? UnknownAuthor : string.Empty;

        if (mode == WorkTextImportMode.WorksWithKnownAuthors)
            author = UnknownAuthor;

        string[] parts = Regex.Split(clean, @"\s+[-–—/]\s+")
            .Select(x => CleanWorkText(x))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        if (parts.Length >= 2)
        {
            title = parts[0];
            author = mode == WorkTextImportMode.WorksBeforeClose
                ? UnknownAuthor
                : string.Join(" - ", parts.Skip(1));
        }
        else if (mode == WorkTextImportMode.ApplyAuthorsAfterClose)
        {
            // Режим раскрытия авторства: строка "01 - Иван Иванов" обновляет только автора.
            title = string.Empty;
            author = clean;
        }

        if (mode == WorkTextImportMode.ApplyAuthorsAfterClose && !string.IsNullOrWhiteSpace(author))
            author = RemoveUnknownAuthorMarker(author);

        return new ContestWork
        {
            Number = number,
            Title = title,
            Author = author,
            Topic = currentTopic
        };
    }

    private static void Upsert(Dictionary<int, ContestWork> byNumber, WorkTextImportResult result, ContestWork work)
    {
        if (work.Number <= 0)
            return;

        if (byNumber.TryGetValue(work.Number, out ContestWork? existing))
        {
            result.DuplicateNumbers++;
            existing.Title = Prefer(work.Title, existing.Title);
            existing.Author = PreferAuthor(work.Author, existing.Author);
            existing.Topic = Prefer(work.Topic, existing.Topic);
            return;
        }

        if (!string.IsNullOrWhiteSpace(work.Author) && !IsUnknownAuthor(work.Author))
            result.AuthorsDetected++;

        byNumber[work.Number] = work;
    }

    private static string Prefer(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string PreferAuthor(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        string clean = value.Trim();
        bool newIsUnknown = IsUnknownAuthor(clean);
        bool oldIsEmptyOrUnknown = string.IsNullOrWhiteSpace(fallback) || IsUnknownAuthor(fallback);
        return !newIsUnknown || oldIsEmptyOrUnknown ? clean : fallback;
    }

    private static bool IsVoteLikeTail(string tail)
    {
        return VoteLikeTailRegex.IsMatch(tail.Trim());
    }

    private static string BuildTopic(string topicTitle, string topicSourceAuthor)
    {
        if (string.IsNullOrWhiteSpace(topicTitle))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(topicSourceAuthor))
            return topicTitle.Trim();

        return topicTitle.Trim() + " - " + topicSourceAuthor.Trim();
    }

    private static string CleanWorkText(string value)
    {
        value = (value ?? string.Empty).Trim();
        value = value.Trim('"', '«', '»', '“', '”', '„');
        value = value.Trim();
        if (value.EndsWith(" -", StringComparison.Ordinal))
            value = value[..^2].Trim();

        return Regex.Replace(value, @"\s+", " ").Trim();
    }

    private static bool IsUnknownAuthor(string value)
    {
        return value.Trim().Equals(UnknownAuthor, StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveUnknownAuthorMarker(string value)
    {
        string clean = CleanWorkText(value);
        return IsUnknownAuthor(clean) ? string.Empty : clean;
    }
}
