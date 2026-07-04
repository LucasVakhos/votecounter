using System.Net;
using System.Text;
using VoteCounter.Models;

namespace VoteCounter.Services;

public sealed class ContestReportExportResult
{
    public string Folder { get; init; } = string.Empty;
    public string SummaryFile { get; set; } = string.Empty;
    public string HtmlFile { get; set; } = string.Empty;
    public string PrintHtmlFile { get; set; } = string.Empty;
    public List<string> Files { get; } = new();
}

public sealed class ContestReportExportService
{
    private readonly VoteAuditService _audit = new();
    private readonly ContestResultsService _results = new();

    public ContestReportExportResult ExportContestPackage(string reportsRoot, Contest contest, IEnumerable<VoteEntry> votes)
    {
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string contestName = MakeSafeLatinFileName($"contest_{contest.Number}_{contest.Name}");
        string folder = Path.Combine(reportsRoot, $"{stamp}_{contestName}");
        Directory.CreateDirectory(folder);

        var result = new ContestReportExportResult { Folder = folder };
        WriteContestFiles(result, folder, contest, votes.ToList(), prefix: string.Empty);
        return result;
    }

    public ContestReportExportResult ExportFirebirdImportSession(
        string reportsRoot,
        FirebirdImportReport report,
        IReadOnlyList<Contest> selectedContests,
        IReadOnlyDictionary<string, List<VoteEntry>> finalVotesByContestId,
        string backupPath,
        int added,
        int updated,
        int importedVoteCount,
        bool replaceVotes,
        bool mergeByNumberAndName,
        bool applySelfVoteRule)
    {
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string folder = Path.Combine(reportsRoot, $"{stamp}_firebird_import");
        Directory.CreateDirectory(folder);

        var result = new ContestReportExportResult { Folder = folder };
        var summary = new StringBuilder();
        summary.AppendLine("ОТЧЁТ ИМПОРТА СТАРОЙ FIREBIRD-БАЗЫ");
        summary.AppendLine("=====================================");
        summary.AppendLine("Дата: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        summary.AppendLine("Режим подключения: " + report.ConnectionMode);
        summary.AppendLine("Рабочая резервная копия: " + (string.IsNullOrWhiteSpace(backupPath) ? "не создавалась" : backupPath));
        summary.AppendLine();
        summary.AppendLine("Настройки импорта:");
        summary.AppendLine("- защита дублей по №/названию: " + YesNo(mergeByNumberAndName));
        summary.AppendLine("- режим голосов: " + (replaceVotes ? "заменить импортированными" : "слить с существующими"));
        summary.AppendLine("- самоголос автора за свою работу = 0: " + YesNo(applySelfVoteRule));
        summary.AppendLine();
        summary.AppendLine("Итог:");
        summary.AppendLine("- выбрано конкурсов: " + selectedContests.Count);
        summary.AppendLine("- добавлено конкурсов: " + added);
        summary.AppendLine("- обновлено конкурсов: " + updated);
        summary.AppendLine("- импортировано голосов: " + importedVoteCount);
        summary.AppendLine("- всего найдено в *.fdb конкурсов: " + report.Contests.Count);
        summary.AppendLine("- всего найдено в *.fdb работ: " + report.WorkCount);
        summary.AppendLine("- всего найдено в *.fdb голосующих: " + report.VoterCount);
        summary.AppendLine("- всего найдено в *.fdb голосов: " + report.VoteCount);
        summary.AppendLine();
        summary.AppendLine("Выбранные конкурсы:");

        foreach (Contest contest in selectedContests.OrderBy(x => ParseNumber(x.Number)).ThenBy(x => x.Name))
        {
            List<VoteEntry> votes = finalVotesByContestId.TryGetValue(contest.Id, out List<VoteEntry>? found)
                ? found
                : new List<VoteEntry>();

            ContestAuditReport audit = _audit.BuildReport(contest, votes);
            ContestResultsReport results = _results.BuildReport(contest, votes);
            summary.AppendLine($"- №{contest.Number} - {contest.Name}: работ {contest.Works.Count}, судей {contest.Voters.Count}, голосов {votes.Count}, должников {audit.Debtors}, неизвестных {audit.UnknownVoters}, самоголосов в 0 {results.SelfVoteCount}");
        }

        if (report.Warnings.Count > 0)
        {
            summary.AppendLine();
            summary.AppendLine("Предупреждения:");
            foreach (string warning in report.Warnings)
                summary.AppendLine("- " + warning);
        }

        string summaryFile = Path.Combine(folder, "import_summary.txt");
        File.WriteAllText(summaryFile, summary.ToString().TrimEnd() + Environment.NewLine, Utf8Bom);
        result.Files.Add(summaryFile);
        result.SummaryFile = summaryFile;

        foreach (Contest contest in selectedContests.OrderBy(x => ParseNumber(x.Number)).ThenBy(x => x.Name))
        {
            List<VoteEntry> votes = finalVotesByContestId.TryGetValue(contest.Id, out List<VoteEntry>? found)
                ? found
                : new List<VoteEntry>();
            string prefix = MakeSafeLatinFileName($"contest_{contest.Number}_{contest.Name}");
            WriteContestFiles(result, folder, contest, votes, prefix);
        }

        return result;
    }

    private void WriteContestFiles(ContestReportExportResult result, string folder, Contest contest, List<VoteEntry> votes, string prefix)
    {
        string safePrefix = string.IsNullOrWhiteSpace(prefix)
            ? MakeSafeLatinFileName($"contest_{contest.Number}_{contest.Name}")
            : prefix;

        ContestResultsReport resultsReport = _results.BuildReport(contest, votes);
        ContestAuditReport auditReport = _audit.BuildReport(contest, votes);

        string finalTxt = Path.Combine(folder, safePrefix + "_final_protocol.txt");
        File.WriteAllText(finalTxt, _results.BuildFinalText(contest, resultsReport) + Environment.NewLine, Utf8Bom);
        result.Files.Add(finalTxt);
        if (string.IsNullOrWhiteSpace(result.SummaryFile))
            result.SummaryFile = finalTxt;

        string ratingCsv = Path.Combine(folder, safePrefix + "_rating.csv");
        WriteCsv(ratingCsv,
            new[] { "Place", "WorkNo", "Title", "Author", "Topic", "Rate", "AcceptedVotes", "Average", "MaxVotes", "SelfVotes" },
            resultsReport.Rows
                .OrderBy(x => x.PlaceNo)
                .ThenBy(x => x.WorkNo)
                .Select(x => new[]
                {
                    x.PlaceText,
                    x.WorkNoText,
                    x.Title,
                    x.Author,
                    x.Topic,
                    x.Rate.ToString(),
                    x.AcceptedVotes.ToString(),
                    x.AverageText,
                    x.MaxVotes.ToString(),
                    x.SelfVotes.ToString()
                }));
        result.Files.Add(ratingCsv);

        string controlCsv = Path.Combine(folder, safePrefix + "_vote_control.csv");
        WriteCsv(controlCsv,
            new[] { "Voter", "Status", "Required", "AcceptedVotes", "MissingWorks", "UnknownWorks", "SelfVotes", "LastVoteAt", "Note" },
            auditReport.Rows.Select(x => new[]
            {
                x.VoterName,
                x.Status,
                x.RequiredToVote ? "yes" : "no",
                x.AcceptedVotes.ToString(),
                x.MissingWorks,
                x.UnknownWorks,
                x.SelfVotes.ToString(),
                x.LastVoteAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                x.Note
            }));
        result.Files.Add(controlCsv);

        string votesCsv = Path.Combine(folder, safePrefix + "_votes.csv");
        WriteCsv(votesCsv,
            new[] { "Voter", "WorkNo", "VotedScoreText", "VotedScore", "AcceptedScoreText", "AcceptedScore", "ChangedByRules", "RuleNote", "Comment", "SourceLine" },
            votes.OrderBy(x => x.VoterName).ThenBy(x => x.WorkNo).Select(x => new[]
            {
                x.VoterName,
                x.WorkNo.ToString("00"),
                string.IsNullOrWhiteSpace(x.VotedScoreText) ? x.OriginalScoreText : x.VotedScoreText,
                (x.VotedScore != 0m ? x.VotedScore : x.OriginalScore).ToString(),
                string.IsNullOrWhiteSpace(x.AcceptedScoreText) ? x.ScoreText : x.AcceptedScoreText,
                (x.AcceptedScore != 0m ? x.AcceptedScore : x.Score).ToString(),
                x.WasChangedByRules ? "yes" : "no",
                x.RuleNote,
                x.Comment,
                x.SourceLine
            }));
        result.Files.Add(votesCsv);

        string html = Path.Combine(folder, safePrefix + "_protocol.html");
        File.WriteAllText(html, BuildProtocolHtml(contest, resultsReport, auditReport, votes, autoPrint: false), Utf8Bom);
        result.Files.Add(html);
        result.HtmlFile = html;

        string printHtml = Path.Combine(folder, safePrefix + "_print.html");
        File.WriteAllText(printHtml, BuildProtocolHtml(contest, resultsReport, auditReport, votes, autoPrint: true), Utf8Bom);
        result.Files.Add(printHtml);
        result.PrintHtmlFile = printHtml;
    }

    private string BuildProtocolHtml(Contest contest, ContestResultsReport resultsReport, ContestAuditReport auditReport, List<VoteEntry> votes, bool autoPrint)
    {
        string title = $"Конкурс №{contest.Number} - {contest.Name}";
        string finalText = _results.BuildFinalText(contest, resultsReport);
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"ru\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine("<title>" + Html(title) + "</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;margin:28px;color:#1f2937;background:#f7f8fb;} .page{max-width:1180px;margin:0 auto;background:white;border:1px solid #d9dee8;border-radius:14px;padding:28px;box-shadow:0 8px 30px rgba(31,41,55,.08);} h1{margin:0 0 6px;font-size:26px;} h2{margin-top:28px;border-bottom:1px solid #e5e7eb;padding-bottom:8px;} .muted{color:#6b7280;} .cards{display:flex;gap:12px;flex-wrap:wrap;margin:18px 0;} .card{background:#f3f6fb;border:1px solid #dbe4f0;border-radius:12px;padding:12px 16px;min-width:150px;} .num{font-size:22px;font-weight:700;} table{border-collapse:collapse;width:100%;margin-top:12px;} th,td{border:1px solid #d9dee8;padding:7px 9px;vertical-align:top;} th{background:#eef3fb;text-align:left;} tr.place1{background:#fff5c8;} tr.place2{background:#eef2f8;} tr.place3{background:#f8e8d8;} tr.debtor{background:#ffe3ea;} tr.unknown{background:#fff3c4;} pre{white-space:pre-wrap;background:#0f172a;color:#f8fafc;border-radius:12px;padding:16px;line-height:1.45;} .ok{color:#15803d;font-weight:600;} .bad{color:#b91c1c;font-weight:600;} .footer{margin-top:24px;font-size:12px;color:#6b7280;} @media print{body{background:white;margin:0}.page{box-shadow:none;border:0;border-radius:0;max-width:none}.no-print{display:none}table{page-break-inside:auto}tr{page-break-inside:avoid;page-break-after:auto}h2{page-break-after:avoid}} ");
        sb.AppendLine("</style>");
        if (autoPrint)
            sb.AppendLine("<script>window.addEventListener('load',function(){setTimeout(function(){window.print();},300);});</script>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"page\">");
        sb.AppendLine("<div class=\"no-print muted\">VoteCounter HTML-протокол. Для печати нажми Ctrl+P, если окно печати не открылось автоматически.</div>");
        sb.AppendLine("<h1>" + Html(title) + "</h1>");
        sb.AppendLine("<div class=\"muted\">Сформировано: " + Html(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")) + "</div>");
        sb.AppendLine("<div class=\"cards\">");
        AppendCard(sb, "Работ", resultsReport.WorkCount.ToString());
        AppendCard(sb, "Судей", resultsReport.VoterCount.ToString());
        AppendCard(sb, "Принято голосов", resultsReport.AcceptedVoteCount.ToString());
        AppendCard(sb, "Самоголосов в 0", resultsReport.SelfVoteCount.ToString());
        AppendCard(sb, "Должников", auditReport.Debtors.ToString());
        AppendCard(sb, "Неизвестных", auditReport.UnknownVoters.ToString());
        sb.AppendLine("</div>");

        sb.AppendLine("<h2>Итоговый протокол</h2>");
        sb.AppendLine("<pre>" + Html(finalText) + "</pre>");

        sb.AppendLine("<h2>Рейтинг работ</h2>");
        sb.AppendLine("<table><thead><tr><th>Место</th><th>№</th><th>Название</th><th>Автор</th><th>Тема</th><th>Баллы</th><th>Голосов</th><th>Средний</th><th>Макс.</th><th>Само</th></tr></thead><tbody>");
        foreach (ContestRatingRow row in resultsReport.Rows.OrderBy(x => x.PlaceNo).ThenBy(x => x.WorkNo))
        {
            string cls = row.PlaceNo <= 3 ? $" class=\"place{row.PlaceNo}\"" : string.Empty;
            sb.AppendLine($"<tr{cls}><td>{Html(row.PlaceText)}</td><td>{Html(row.WorkNoText)}</td><td>{Html(row.Title)}</td><td>{Html(row.Author)}</td><td>{Html(row.Topic)}</td><td>{row.Rate}</td><td>{row.AcceptedVotes}</td><td>{Html(row.AverageText)}</td><td>{row.MaxVotes}</td><td>{row.SelfVotes}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");

        sb.AppendLine("<h2>Контроль голосования</h2>");
        sb.AppendLine("<table><thead><tr><th>Голосующий</th><th>Статус</th><th>Обязан</th><th>Принято</th><th>Не хватает</th><th>Неизвестные №</th><th>Само</th><th>Последний голос</th><th>Примечание</th></tr></thead><tbody>");
        foreach (VoterStatusRow row in auditReport.Rows.OrderBy(x => x.IsUnknownVoter).ThenByDescending(x => x.IsDebtor).ThenBy(x => x.VoterName))
        {
            string cls = row.IsUnknownVoter ? " class=\"unknown\"" : row.IsDebtor ? " class=\"debtor\"" : string.Empty;
            string required = row.RequiredToVote ? "да" : "нет";
            sb.AppendLine($"<tr{cls}><td>{Html(row.VoterName)}</td><td>{Html(row.Status)}</td><td>{required}</td><td>{row.AcceptedVotes}</td><td>{Html(row.MissingWorks)}</td><td>{Html(row.UnknownWorks)}</td><td>{row.SelfVotes}</td><td>{Html(row.LastVoteAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty)}</td><td>{Html(row.Note)}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");

        sb.AppendLine("<h2>Голоса</h2>");
        sb.AppendLine("<table><thead><tr><th>Голосующий</th><th>№</th><th>Оценка</th><th>Баллы</th><th>Исходно</th><th>Правило</th><th>Комментарий</th></tr></thead><tbody>");
        foreach (VoteEntry vote in votes.OrderBy(x => x.VoterName).ThenBy(x => x.WorkNo))
        {
            string rule = vote.WasChangedByRules ? vote.RuleNote : string.Empty;
            sb.AppendLine($"<tr><td>{Html(vote.VoterName)}</td><td>{vote.WorkNo:00}</td><td>{Html(vote.ScoreText)}</td><td>{vote.Score}</td><td>{vote.OriginalScore}</td><td>{Html(rule)}</td><td>{Html(vote.Comment)}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");
        sb.AppendLine("<div class=\"footer\">VoteCounter - HTML Export Pack</div>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static void AppendCard(StringBuilder sb, string caption, string value)
    {
        sb.AppendLine("<div class=\"card\"><div class=\"muted\">" + Html(caption) + "</div><div class=\"num\">" + Html(value) + "</div></div>");
    }

    private static string Html(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static void WriteCsv(string path, IEnumerable<string> headers, IEnumerable<IEnumerable<string>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("sep=;");
        sb.AppendLine(string.Join(";", headers.Select(EscapeCsv)));
        foreach (IEnumerable<string> row in rows)
            sb.AppendLine(string.Join(";", row.Select(EscapeCsv)));
        File.WriteAllText(path, sb.ToString(), Utf8Bom);
    }

    private static string EscapeCsv(string? value)
    {
        string text = value ?? string.Empty;
        text = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
        if (text.Contains(';') || text.Contains('"') || text.Contains(' '))
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        return text;
    }

    public static string MakeSafeLatinFileName(string? value)
    {
        string source = string.IsNullOrWhiteSpace(value) ? "report" : value.Trim();
        var sb = new StringBuilder(source.Length);
        foreach (char ch in source)
        {
            string mapped = Transliterate(ch);
            foreach (char c in mapped)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
                else if (c is '_' or '-' or '.')
                    sb.Append(c);
                else if (char.IsWhiteSpace(c))
                    sb.Append('_');
            }
        }

        string result = sb.ToString().Trim('_', '.', '-');
        while (result.Contains("__", StringComparison.Ordinal))
            result = result.Replace("__", "_");

        return string.IsNullOrWhiteSpace(result) ? "report" : result;
    }

    private static string Transliterate(char ch)
    {
        return ch switch
        {
            'А' => "A", 'Б' => "B", 'В' => "V", 'Г' => "G", 'Д' => "D", 'Е' => "E", 'Ё' => "Yo", 'Ж' => "Zh", 'З' => "Z", 'И' => "I", 'Й' => "Y",
            'К' => "K", 'Л' => "L", 'М' => "M", 'Н' => "N", 'О' => "O", 'П' => "P", 'Р' => "R", 'С' => "S", 'Т' => "T", 'У' => "U", 'Ф' => "F",
            'Х' => "Kh", 'Ц' => "Ts", 'Ч' => "Ch", 'Ш' => "Sh", 'Щ' => "Sch", 'Ъ' => "", 'Ы' => "Y", 'Ь' => "", 'Э' => "E", 'Ю' => "Yu", 'Я' => "Ya",
            'а' => "a", 'б' => "b", 'в' => "v", 'г' => "g", 'д' => "d", 'е' => "e", 'ё' => "yo", 'ж' => "zh", 'з' => "z", 'и' => "i", 'й' => "y",
            'к' => "k", 'л' => "l", 'м' => "m", 'н' => "n", 'о' => "o", 'п' => "p", 'р' => "r", 'с' => "s", 'т' => "t", 'у' => "u", 'ф' => "f",
            'х' => "kh", 'ц' => "ts", 'ч' => "ch", 'ш' => "sh", 'щ' => "sch", 'ъ' => "", 'ы' => "y", 'ь' => "", 'э' => "e", 'ю' => "yu", 'я' => "ya",
            _ => ch.ToString()
        };
    }

    private static int ParseNumber(string? value)
    {
        return int.TryParse(value, out int number) ? number : int.MaxValue;
    }

    private static string YesNo(bool value) => value ? "да" : "нет";

    private static readonly Encoding Utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
}
