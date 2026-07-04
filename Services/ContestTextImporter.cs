using System.Text;
using System.Text.RegularExpressions;
using VoteCounter.Models;

namespace VoteCounter.Services;

public sealed class ContestTextImportResult
{
    public Contest Contest { get; set; } = new();
    public List<VoteEntry> Votes { get; } = new();
    public List<string> Warnings { get; } = new();
    public string SourceTitle { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string DeadlineText { get; set; } = string.Empty;

    public int WorkCount => Contest.Works.Count;
    public int VoterCount => Contest.Voters.Count;
    public int VoteCount => Votes.Count;

    public string BuildPreviewText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"№ конкурса: {Contest.Number}");
        sb.AppendLine($"Название: {Contest.Name}");
        sb.AppendLine($"Режим: {(Contest.HostKnowsAuthors ? "ведущий знает авторов" : "счётчик до раскрытия, авторы скрыты")}");
        if (!string.IsNullOrWhiteSpace(HostName))
            sb.AppendLine($"Ведущий: {HostName}");
        if (!string.IsNullOrWhiteSpace(DeadlineText))
            sb.AppendLine($"Дедлайн/срок: {DeadlineText}");
        sb.AppendLine($"Работ найдено: {WorkCount}");
        sb.AppendLine($"Судей/голосующих найдено: {VoterCount}");
        sb.AppendLine($"Голосов найдено: {VoteCount}");

        if (Contest.Works.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Первые работы:");
            foreach (ContestWork work in Contest.Works.OrderBy(x => x.Number).Take(12))
            {
                string author = string.IsNullOrWhiteSpace(work.Author) ? "автор не указан" : work.Author;
                string title = string.IsNullOrWhiteSpace(work.Title) ? "без названия" : work.Title;
                sb.AppendLine($"{work.Number:00}. {title} - {author}");
            }
        }

        if (Contest.Voters.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Первые судьи:");
            foreach (VoterSetting voter in Contest.Voters.Take(12))
                sb.AppendLine("- " + voter.Name);
        }

        if (Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Предупреждения:");
            foreach (string warning in Warnings.Take(20))
                sb.AppendLine("! " + warning);
        }

        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// Импортирует конкурс целиком из одного большого текста: карточка конкурса, список работ,
/// авторы, голосующие и, при желании, уже присланные голоса.
/// </summary>
public sealed class ContestTextImporter
{
    private readonly WorkTextImporter _workImporter = new();
    private readonly VoteParser _voteParser = new();
    private readonly VoteRuleService _rules = new();
    private readonly ContestRulesAutoFixService _rulesAutoFix = new();

    private static readonly Regex ContestNumberRegex = new(
        @"(?:конкурс|состязание|тур|этап)\s*(?:№|N|No\.?|номер)?\s*(?<no>\d{1,4})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ContestNumberBeforeWordRegex = new(
        @"\b(?<no>\d{1,4})\s*(?:й|ый|ой|ий|[-–—]?(?:й|ый|ой|ий))?\s+(?:(?:\S+)\s+){0,8}?(?:конкурс|состязание|тур|этап)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ContestLineQuotedTitleRegex = new(
        @"(?:конкурс|состязание|тур|этап)\b.*?[""«“„](?<title>[^""»”]+)[""»”]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex QuotedTitleRegex = new(
        @"^[""«“„](?<title>[^""»”]{4,140})[""»”]$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ExplicitTitleRegex = new(
        @"^(?:название\s+конкурса|конкурс|состязание|тема)\s*[:\-]\s*(?<title>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex HostRegex = new(
        @"^(?:ведущ(?:ий|ая)|организатор|куратор|проводит|принимает)\s*[:\-]\s*(?<host>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex HostPlainRegex = new(
        @"^ведущ(?:ий|ая)\s+(?<host>.+?)(?:[\.!]+)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex DeadlineRegex = new(
        @"(?<deadline>(?:до|при[её]м\s+до|голосование\s+до|срок\s+до)\s+.{3,80})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public ContestTextImportResult Parse(
        string? text,
        string defaultContestNumber,
        bool hostKnowsAuthors,
        bool importVotes,
        bool authorsAsVoters)
    {
        string source = text ?? string.Empty;
        var result = new ContestTextImportResult();
        var contest = new Contest
        {
            Number = DetectContestNumber(source, defaultContestNumber),
            Name = DetectContestName(source),
            HostKnowsAuthors = hostKnowsAuthors,
            StartedAt = DateTime.Now,
            ClosedAt = null,
            IsActive = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        result.Contest = contest;
        result.SourceTitle = contest.Name;
        result.HostName = DetectHost(source);
        result.DeadlineText = DetectDeadline(source);

        WorkTextImportMode mode = hostKnowsAuthors
            ? WorkTextImportMode.WorksWithKnownAuthors
            : WorkTextImportMode.WorksBeforeClose;

        WorkTextImportResult works = _workImporter.Parse(source, mode);
        contest.Works = works.Works
            .Where(x => x.Number > 0)
            .OrderBy(x => x.Number)
            .ThenBy(x => x.Title)
            .Select(x => x.Clone())
            .ToList();
        result.Warnings.AddRange(works.Warnings);

        ImportResult? voteResult = null;
        if (importVotes)
        {
            _rulesAutoFix.EnsureRules(contest, source, result.Warnings);
            voteResult = _voteParser.Parse(source, contest.Id, contest);
            _rules.Apply(contest, voteResult);
            foreach (ParsedVoteBlock block in voteResult.Blocks)
            {
                foreach (VoteEntry vote in block.Votes)
                    result.Votes.Add(vote);
            }

            result.Warnings.AddRange(voteResult.Warnings);
            EnsureWorksFromVotes(contest, result.Votes, hostKnowsAuthors);
            AddVotersFromVoteBlocks(contest, voteResult.Blocks);
        }

        if (authorsAsVoters)
            AddAuthorsAsVoters(contest);

        if (contest.Works.Count == 0)
            result.Warnings.Add("Список работ не найден. Можно создать конкурс и затем добавить работы через вкладку \"Настройки конкурса\".");

        if (contest.Voters.Count == 0)
            result.Warnings.Add("Судьи/голосующие не найдены. Можно добавить их позже вручную или импортом голосов.");

        return result;
    }

    private static void EnsureWorksFromVotes(Contest contest, IEnumerable<VoteEntry> votes, bool hostKnowsAuthors)
    {
        var known = new HashSet<int>(contest.Works.Where(x => x.Number > 0).Select(x => x.Number));
        foreach (int number in votes.Select(x => x.WorkNo).Where(x => x > 0).Distinct().OrderBy(x => x))
        {
            if (!known.Add(number))
                continue;

            contest.Works.Add(new ContestWork
            {
                Number = number,
                Title = string.Empty,
                Author = hostKnowsAuthors ? string.Empty : WorkTextImporter.UnknownAuthor
            });
        }

        contest.Works = contest.Works.OrderBy(x => x.Number).ThenBy(x => x.Title).ToList();
    }

    private static void AddVotersFromVoteBlocks(Contest contest, IEnumerable<ParsedVoteBlock> blocks)
    {
        var existing = new HashSet<string>(
            contest.Voters.Select(x => NameNormalizer.Normalize(x.Name)).Where(x => !string.IsNullOrWhiteSpace(x)),
            StringComparer.OrdinalIgnoreCase);

        foreach (ParsedVoteBlock block in blocks.OrderBy(x => x.VoterName))
        {
            string name = (block.VoterName ?? string.Empty).Trim();
            string key = NameNormalizer.Normalize(name);
            if (string.IsNullOrWhiteSpace(key) || !existing.Add(key))
                continue;

            contest.Voters.Add(new VoterSetting { Name = name, MustVote = true });
        }
    }

    private static void AddAuthorsAsVoters(Contest contest)
    {
        var existing = new HashSet<string>(
            contest.Voters.Select(x => NameNormalizer.Normalize(x.Name)).Where(x => !string.IsNullOrWhiteSpace(x)),
            StringComparer.OrdinalIgnoreCase);

        foreach (ContestWork work in contest.Works.OrderBy(x => x.Number))
        {
            string name = (work.Author ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Equals(WorkTextImporter.UnknownAuthor, StringComparison.OrdinalIgnoreCase))
                continue;

            string key = NameNormalizer.Normalize(name);
            if (string.IsNullOrWhiteSpace(key) || !existing.Add(key))
                continue;

            contest.Voters.Add(new VoterSetting { Name = name, MustVote = true });
        }
    }

    private static string DetectContestNumber(string source, string fallback)
    {
        string text = source ?? string.Empty;

        Match match = ContestNumberRegex.Match(text);
        if (match.Success)
            return int.Parse(match.Groups["no"].Value).ToString("000");

        // Частый формат из ОК/форумов:
        // "178й 2х недельный стихотворный конкурс "Название" начинается!"
        match = ContestNumberBeforeWordRegex.Match(text);
        if (match.Success)
            return int.Parse(match.Groups["no"].Value).ToString("000");

        return string.IsNullOrWhiteSpace(fallback) ? "001" : fallback.Trim();
    }

    private static string DetectContestName(string source)
    {
        List<string> lines = SplitUsefulLines(source).ToList();

        // Самый надёжный вариант для объявлений конкурсов:
        // 178й 2х недельный стихотворный конкурс "Нарисуй мне, художник, картину!" начинается!
        foreach (string line in lines)
        {
            Match match = ContestLineQuotedTitleRegex.Match(line);
            if (match.Success)
            {
                string title = CleanTitle(match.Groups["title"].Value);
                if (!string.IsNullOrWhiteSpace(title))
                    return title;
            }
        }

        foreach (string line in lines)
        {
            Match match = ExplicitTitleRegex.Match(line);
            if (match.Success)
            {
                string title = CleanTitle(match.Groups["title"].Value);
                if (!string.IsNullOrWhiteSpace(title) && !IsServiceContestLine(title))
                    return title;
            }
        }

        foreach (string line in lines)
        {
            Match match = QuotedTitleRegex.Match(line);
            if (match.Success)
            {
                string title = CleanTitle(match.Groups["title"].Value);
                if (!string.IsNullOrWhiteSpace(title) && !IsServiceContestLine(title))
                    return title;
            }
        }

        foreach (string line in lines)
        {
            if (LooksLikeContestName(line))
                return CleanTitle(line);
        }

        return "Новый конкурс из текста";
    }

    private static string DetectHost(string source)
    {
        foreach (string line in SplitUsefulLines(source))
        {
            Match match = HostRegex.Match(line);
            if (match.Success)
                return CleanHost(match.Groups["host"].Value);

            match = HostPlainRegex.Match(line);
            if (match.Success)
                return CleanHost(match.Groups["host"].Value);
        }

        return string.Empty;
    }

    private static string DetectDeadline(string source)
    {
        Match match = DeadlineRegex.Match(source ?? string.Empty);
        return match.Success ? CleanTitle(match.Groups["deadline"].Value) : string.Empty;
    }

    private static IEnumerable<string> SplitUsefulLines(string? text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => x != ".")
            .Where(x => !x.All(ch => ch == '-' || ch == '–' || ch == '—' || char.IsWhiteSpace(ch)))
            .Where(x => !Regex.IsMatch(x, @"^\d{1,3}\s*[-–—]\s*[0-4](?:\s*[+\-−])?\b"));
    }

    private static bool LooksLikeContestName(string line)
    {
        if (line.Length < 4 || line.Length > 140)
            return false;

        if (IsServiceContestLine(line))
            return false;

        string lower = line.ToLowerInvariant();
        if (lower.StartsWith("ведущ") || lower.StartsWith("организатор") || lower.StartsWith("приём") || lower.StartsWith("прием") || lower.StartsWith("голосование"))
            return false;

        if (Regex.IsMatch(line, @"^№?\s*\d{1,4}(?:\s*[\.)\-–—:]|\s*$)"))
            return false;

        if (line.Contains("конкурс", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("состяз", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("тема", StringComparison.OrdinalIgnoreCase))
            return !IsGenericContestAnnouncement(line);

        int letters = line.Count(char.IsLetter);
        int upper = line.Count(ch => char.IsLetter(ch) && char.IsUpper(ch));
        return letters >= 6 && upper >= Math.Max(4, letters / 2);
    }

    private static bool IsGenericContestAnnouncement(string line)
    {
        string lower = line.ToLowerInvariant();
        return lower.Contains("начинается") ||
               lower.StartsWith("задания конкурса") ||
               lower.StartsWith("условия конкурса") ||
               lower.StartsWith("правила конкурса") ||
               lower.StartsWith("конкурс находится") ||
               lower.StartsWith("приём") ||
               lower.StartsWith("прием");
    }

    private static bool IsServiceContestLine(string line)
    {
        string lower = CleanTitle(line).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(lower))
            return true;

        return lower is "уважаемые авторы" or "дорогие авторы" or "авторы" ||
               lower.StartsWith("уважаемые авторы") ||
               lower.StartsWith("задания конкурса") ||
               lower.StartsWith("просьба") ||
               lower.StartsWith("авторы помните") ||
               lower.StartsWith("желаем всем") ||
               lower.StartsWith("конкурс находится") ||
               lower.StartsWith("ведущему") ||
               lower.StartsWith("ответить");
    }

    private static string CleanTitle(string value)
    {
        string clean = (value ?? string.Empty).Trim();
        clean = clean.Trim('"', '«', '»', '“', '”', '„');
        clean = Regex.Replace(clean, @"\s+", " ").Trim();
        clean = Regex.Replace(clean, @"^(?:№\s*)?\d{1,4}\s*[-–—:\.)]\s*", string.Empty).Trim();
        return clean.Trim('-', '–', '—', ':', '.', ' ');
    }

    private static string CleanHost(string value)
    {
        string clean = CleanTitle(value);
        clean = Regex.Replace(clean, @"\s+можно\s+написать.*$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
        return clean.Trim('-', '–', '—', ':', '.', ' ');
    }
}
