using System.Text;
using VoteCounter.Models;

namespace VoteCounter.Services;

public sealed class ContestResultsService
{
    public ContestResultsReport BuildReport(Contest contest, IEnumerable<VoteEntry> allVotes)
    {
        var report = new ContestResultsReport();

        var works = contest.Works
            .Where(x => x.Number > 0)
            .GroupBy(x => x.Number)
            .Select(g => g.Last())
            .OrderBy(x => x.Number)
            .ToList();

        var workByNo = works.ToDictionary(x => x.Number);
        var votes = allVotes
            .Where(x => workByNo.ContainsKey(x.WorkNo))
            .GroupBy(x => (x.VoterKey, x.WorkNo))
            .Select(g => g.Last())
            .ToList();

        report.VoterCount = votes
            .Select(x => x.VoterKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var rows = new List<ContestRatingRow>();
        foreach (ContestWork work in works)
        {
            List<VoteEntry> workVotes = votes
                .Where(x => x.WorkNo == work.Number)
                .ToList();

            int selfVotes = workVotes.Count(x => contest.TreatSelfVoteAsZero && NameNormalizer.Same(x.VoterName, work.Author));
            List<VoteEntry> acceptedVotes = workVotes
                .Where(x => !(contest.TreatSelfVoteAsZero && NameNormalizer.Same(x.VoterName, work.Author)))
                .ToList();

            decimal rate = acceptedVotes.Sum(x => x.Score);
            int acceptedCount = acceptedVotes.Count;

            rows.Add(new ContestRatingRow
            {
                WorkNo = work.Number,
                Title = work.Title ?? string.Empty,
                Author = work.Author ?? string.Empty,
                Topic = work.Topic ?? string.Empty,
                Rate = rate,
                AcceptedVotes = acceptedCount,
                SelfVotes = selfVotes,
                MaxVotes = acceptedVotes.Count(x => x.Score == contest.MaxVote),
                Average = acceptedCount == 0 ? 0m : rate / acceptedCount
            });
        }

        AssignPlaces(rows);
        report.Rows.AddRange(rows);
        report.AcceptedVoteCount = rows.Sum(x => x.AcceptedVotes);
        report.SelfVoteCount = rows.Sum(x => x.SelfVotes);
        return report;
    }

    public string BuildFinalText(Contest contest, ContestResultsReport report)
    {
        var sb = new StringBuilder();
        string number = string.IsNullOrWhiteSpace(contest.Number) ? string.Empty : $" №{contest.Number}";
        string name = string.IsNullOrWhiteSpace(contest.Name) ? "Конкурс" : contest.Name.Trim();

        sb.AppendLine($"ИТОГИ{namePrefix(number)}{name}");
        sb.AppendLine("------------------------");
        sb.AppendLine($"Проголосовало судей: {report.VoterCount}");
        sb.AppendLine($"Работ в конкурсе: {report.WorkCount}");
        sb.AppendLine($"Принято голосов: {report.AcceptedVoteCount}");
        if (report.SelfVoteCount > 0)
            sb.AppendLine($"Самоголосование учтено как 0: {report.SelfVoteCount}");
        sb.AppendLine();
        sb.AppendLine("Рейтинг:");

        foreach (ContestRatingRow row in report.Rows.OrderBy(x => x.PlaceNo).ThenBy(x => x.WorkNo))
        {
            string title = string.IsNullOrWhiteSpace(row.Title) ? "без названия" : row.Title;
            string author = string.IsNullOrWhiteSpace(row.Author) ? "автор не указан" : row.Author;
            sb.AppendLine($"{row.PlaceText}. №{row.WorkNoText} - {author} - \"{title}\" - {row.Rate}");
        }

        List<ContestRatingRow> winners = report.Rows
            .Where(x => x.PlaceNo <= 3)
            .OrderBy(x => x.PlaceNo)
            .ThenBy(x => x.WorkNo)
            .ToList();

        if (winners.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Победители:");
            foreach (ContestRatingRow row in winners)
                sb.AppendLine($"{Medal(row.PlaceNo)} {row.PlaceText} место - №{row.WorkNoText}, {row.Author}, {row.Rate}");
        }

        return sb.ToString().TrimEnd();

        static string namePrefix(string numberText) => string.IsNullOrWhiteSpace(numberText) ? ": " : $"{numberText}: ";
    }

    public string BuildWinnersText(ContestResultsReport report)
    {
        var winners = report.Rows
            .Where(x => x.PlaceNo <= 3)
            .OrderBy(x => x.PlaceNo)
            .ThenBy(x => x.WorkNo)
            .ToList();

        if (winners.Count == 0)
            return "Победителей пока нет.";

        var sb = new StringBuilder();
        foreach (ContestRatingRow row in winners)
            sb.AppendLine($"{Medal(row.PlaceNo)} {row.PlaceText} место - №{row.WorkNoText}, {row.Author}, {row.Rate}");
        return sb.ToString().TrimEnd();
    }

    private static void AssignPlaces(List<ContestRatingRow> rows)
    {
        var ordered = rows
            .OrderByDescending(x => x.Rate)
            .ThenBy(x => x.WorkNo)
            .ToList();

        int index = 0;
        foreach (IGrouping<decimal, ContestRatingRow> group in ordered.GroupBy(x => x.Rate).OrderByDescending(x => x.Key))
        {
            int from = index + 1;
            int to = index + group.Count();
            foreach (ContestRatingRow row in group)
            {
                row.PlaceNo = from;
                row.PlaceTo = to;
            }
            index = to;
        }

        rows.Sort((a, b) =>
        {
            int cmp = a.PlaceNo.CompareTo(b.PlaceNo);
            return cmp != 0 ? cmp : a.WorkNo.CompareTo(b.WorkNo);
        });
    }

    private static string Medal(int placeNo)
    {
        return placeNo switch
        {
            1 => "🥇",
            2 => "🥈",
            3 => "🥉",
            _ => "•"
        };
    }
}
