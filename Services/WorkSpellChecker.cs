using System.Text.RegularExpressions;
using VoteCounter.Models;

namespace VoteCounter.Services;

/// <summary>
/// Лёгкая проверка текста перед приёмом работы.
/// Это не тяжёлый внешний корректор и не сетевой Yandex Speller из IDE-плагина,
/// а нужный минимум для приёмки: явные опечатки, смешанная раскладка,
/// кавычки/скобки/пунктуация и синтаксис оформления.
/// </summary>
public sealed class WorkSpellChecker
{
    private static readonly Regex MixedLayoutRegex = new(@"[А-Яа-яЁё][A-Za-z]+|[A-Za-z]+[А-Яа-яЁё]", RegexOptions.Compiled);
    private static readonly Regex DuplicatePunctuationRegex = new(@"(,,|;;|!!{1,}|\?\?{1,})", RegexOptions.Compiled);
    private static readonly Regex WordRegex = new(@"[А-Яа-яЁёA-Za-z-]+", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> CommonTypos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["джентельмен"] = "джентльмен",
        ["джентельмена"] = "джентльмена",
        ["джентельменом"] = "джентльменом",
        ["всёж"] = "всё ж",
        ["всеж"] = "всё ж",
        ["все-ж"] = "всё ж",
        ["всё-ж"] = "всё ж",
        ["впрочим"] = "впрочем",
        ["упрочем"] = "впрочем",
        ["путкты"] = "пункты",
        ["путкт"] = "пункт"
    };

    public WorkSpellCheckReport CheckSubmission(SingleWorkSubmission submission)
    {
        return CheckWork(new ContestWork
        {
            Number = submission.Number,
            Title = submission.Title,
            Author = submission.Author,
            Topic = submission.Topic,
            Content = submission.Content
        });
    }

    public WorkSpellCheckReport CheckWorks(IEnumerable<ContestWork> works)
    {
        var report = new WorkSpellCheckReport();
        foreach (ContestWork work in works)
        {
            WorkSpellCheckReport one = CheckWork(work);
            foreach (WorkSpellIssue issue in one.Issues)
            {
                issue.Fragment = string.IsNullOrWhiteSpace(issue.Fragment)
                    ? $"№{work.Number:00} {work.Title}"
                    : $"№{work.Number:00} {work.Title}: {issue.Fragment}";
                report.Issues.Add(issue);
            }
        }

        return report;
    }

    public WorkSpellCheckReport CheckWork(ContestWork work)
    {
        var report = new WorkSpellCheckReport();
        string text = work.Content ?? string.Empty;
        string title = work.Title ?? string.Empty;

        if (string.IsNullOrWhiteSpace(title))
        {
            Add(report, WorkSpellIssueSeverity.Error, 0, string.Empty, "нет названия работы", "первая содержательная строка должна быть заголовком");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            Add(report, WorkSpellIssueSeverity.Error, 0, title, "нет полного текста работы", "вставь текст перед сохранением");
            return report;
        }

        CheckWholeText(report, text);
        CheckLines(report, text);
        return report;
    }

    private static void CheckWholeText(WorkSpellCheckReport report, string text)
    {
        int straightQuotes = text.Count(x => x == '"');
        if (straightQuotes % 2 != 0)
            Add(report, WorkSpellIssueSeverity.Warning, 0, "\"", "нечётное количество кавычек", "проверь открывающую/закрывающую кавычку");

        if (text.Count(x => x == '(') != text.Count(x => x == ')'))
            Add(report, WorkSpellIssueSeverity.Warning, 0, "()", "количество открывающих и закрывающих скобок не совпадает", "проверь тему в скобках и авторские ремарки");

        if (text.Contains('—'))
            Add(report, WorkSpellIssueSeverity.Info, 0, "—", "найден длинный тире", "по твоему формату лучше заменить на короткий дефис -");

        if (text.Contains('«') || text.Contains('»'))
            Add(report, WorkSpellIssueSeverity.Info, 0, "«...»", "найдены русские кавычки-ёлочки", "по твоему формату лучше заменить на стандартные кавычки \"...\"");
    }

    private static void CheckLines(WorkSpellCheckReport report, string text)
    {
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int contentLines = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            int lineNo = i + 1;
            string line = lines[i];
            string trimmed = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed) && trimmed != ".")
                contentLines++;

            if (line.Length != line.TrimEnd().Length)
                Add(report, WorkSpellIssueSeverity.Info, lineNo, Shorten(trimmed), "лишние пробелы в конце строки", "убрать хвостовые пробелы");

            if (line.Contains('\t'))
                Add(report, WorkSpellIssueSeverity.Info, lineNo, Shorten(trimmed), "найдена табуляция", "заменить табуляцию обычным пробелом");

            Match mixed = MixedLayoutRegex.Match(line);
            if (mixed.Success)
                Add(report, WorkSpellIssueSeverity.Warning, lineNo, mixed.Value, "похоже на смешанную русско-латинскую раскладку", "проверь буквы, похожие на русские/английские");

            Match punct = DuplicatePunctuationRegex.Match(line);
            if (punct.Success)
                Add(report, WorkSpellIssueSeverity.Info, lineNo, punct.Value, "подозрительное повторение знаков препинания", "оставить только если это авторский приём");

            if (trimmed.Length > 95)
                Add(report, WorkSpellIssueSeverity.Info, lineNo, Shorten(trimmed), "очень длинная строка", "проверь перенос, чтобы работа красиво легла в протокол");

            foreach (Match word in WordRegex.Matches(line))
            {
                if (CommonTypos.TryGetValue(word.Value.Trim('-'), out string? suggestion))
                    Add(report, WorkSpellIssueSeverity.Warning, lineNo, word.Value, "возможная опечатка", suggestion);
            }
        }

        if (contentLines < 2)
            Add(report, WorkSpellIssueSeverity.Warning, 0, string.Empty, "слишком мало содержательных строк", "проверь, что вставлена вся работа, а не только заголовок");
    }

    private static void Add(WorkSpellCheckReport report, WorkSpellIssueSeverity severity, int lineNo, string fragment, string message, string suggestion)
    {
        report.Issues.Add(new WorkSpellIssue
        {
            Severity = severity,
            LineNo = lineNo,
            Fragment = fragment,
            Message = message,
            Suggestion = suggestion
        });
    }

    private static string Shorten(string value)
    {
        value = (value ?? string.Empty).Trim();
        return value.Length <= 80 ? value : value[..77] + "...";
    }
}
