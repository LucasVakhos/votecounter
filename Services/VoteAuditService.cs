using System.Text;
using VoteCounter.Models;

namespace VoteCounter.Services;

public sealed class VoteAuditService
{
    public ContestAuditReport BuildReport(Contest contest, IEnumerable<VoteEntry> allVotes)
    {
        var report = new ContestAuditReport();
        var votes = allVotes.ToList();
        var works = contest.Works
            .Where(x => x.Number > 0)
            .OrderBy(x => x.Number)
            .ToList();

        var knownWorkNumbers = works.Select(x => x.Number).Distinct().OrderBy(x => x).ToList();
        var knownWorkSet = knownWorkNumbers.ToHashSet();
        report.WorkCount = knownWorkNumbers.Count;
        report.AcceptedVoteCount = votes.Count(x => knownWorkSet.Count == 0 || knownWorkSet.Contains(x.WorkNo));

        var authorsByWorkNo = works
            .Where(x => !string.IsNullOrWhiteSpace(x.Author))
            .GroupBy(x => x.Number)
            .ToDictionary(g => g.Key, g => NameNormalizer.Normalize(g.Last().Author));

        var expected = contest.Voters
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => NameNormalizer.Normalize(x.Name), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .OrderBy(x => x.Name)
            .ToList();

        if (expected.Count == 0)
        {
            expected = votes
                .GroupBy(x => x.VoterKey, StringComparer.OrdinalIgnoreCase)
                .Select(g => new VoterSetting { Name = g.Last().VoterName, MustVote = true })
                .OrderBy(x => x.Name)
                .ToList();
        }

        var expectedKeys = new HashSet<string>(
            expected.Select(x => NameNormalizer.Normalize(x.Name)).Where(x => !string.IsNullOrWhiteSpace(x)),
            StringComparer.OrdinalIgnoreCase);

        foreach (VoterSetting voter in expected)
        {
            string key = NameNormalizer.Normalize(voter.Name);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            List<VoteEntry> voterVotes = votes
                .Where(x => x.VoterKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.WorkNo)
                .Select(g => g.Last())
                .OrderBy(x => x.WorkNo)
                .ToList();

            var votedKnown = voterVotes
                .Where(x => knownWorkSet.Count == 0 || knownWorkSet.Contains(x.WorkNo))
                .Select(x => x.WorkNo)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var votedUnknown = voterVotes
                .Where(x => knownWorkSet.Count > 0 && !knownWorkSet.Contains(x.WorkNo))
                .Select(x => x.WorkNo)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var missing = knownWorkNumbers
                .Where(x => !votedKnown.Contains(x))
                .ToList();

            int selfVotes = voterVotes.Count(v =>
                authorsByWorkNo.TryGetValue(v.WorkNo, out string? authorKey) &&
                !string.IsNullOrWhiteSpace(authorKey) &&
                authorKey.Equals(key, StringComparison.OrdinalIgnoreCase));

            var row = new VoterStatusRow
            {
                VoterName = voter.Name,
                RequiredToVote = voter.MustVote,
                AcceptedVotes = votedKnown.Count,
                KnownVotes = voterVotes.Count,
                MissingCount = missing.Count,
                MissingWorks = FormatNumbers(missing),
                UnknownWorks = FormatNumbers(votedUnknown),
                SelfVotes = selfVotes,
                LastVoteAt = voterVotes.Count == 0 ? null : voterVotes.Max(x => x.UpdatedAt)
            };

            row.Status = BuildStatus(row, knownWorkNumbers.Count);
            row.Note = BuildNote(row, knownWorkNumbers.Count);
            report.Rows.Add(row);
        }

        var unknownVoters = votes
            .GroupBy(x => x.VoterKey, StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && !expectedKeys.Contains(g.Key))
            .Select(g => g.ToList())
            .OrderBy(g => g.Last().VoterName)
            .ToList();

        foreach (List<VoteEntry> voterVotes in unknownVoters)
        {
            var known = voterVotes
                .Where(x => knownWorkSet.Count == 0 || knownWorkSet.Contains(x.WorkNo))
                .Select(x => x.WorkNo)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var unknown = voterVotes
                .Where(x => knownWorkSet.Count > 0 && !knownWorkSet.Contains(x.WorkNo))
                .Select(x => x.WorkNo)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            report.Rows.Add(new VoterStatusRow
            {
                VoterName = voterVotes.Last().VoterName,
                RequiredToVote = false,
                AcceptedVotes = known.Count,
                KnownVotes = voterVotes.Count,
                MissingCount = 0,
                MissingWorks = string.Empty,
                UnknownWorks = FormatNumbers(unknown),
                SelfVotes = 0,
                LastVoteAt = voterVotes.Max(x => x.UpdatedAt),
                Status = "Голосовал, но не в списке",
                Note = "Добавь в список голосующих, если это судья."
            });
        }

        report.Rows.Sort((a, b) =>
        {
            int groupA = a.IsUnknownVoter ? 2 : a.IsDebtor ? 0 : 1;
            int groupB = b.IsUnknownVoter ? 2 : b.IsDebtor ? 0 : 1;
            int cmp = groupA.CompareTo(groupB);
            return cmp != 0 ? cmp : string.Compare(a.VoterName, b.VoterName, StringComparison.CurrentCultureIgnoreCase);
        });

        return report;
    }

    public string BuildDebtorsText(ContestAuditReport report)
    {
        var debtors = report.Rows
            .Where(x => x.IsDebtor && !x.IsUnknownVoter)
            .OrderBy(x => x.VoterName)
            .ToList();

        if (debtors.Count == 0)
            return "Должников по голосованию нет.";

        var sb = new StringBuilder();
        sb.AppendLine("Не проголосовали / неполное голосование:");
        foreach (VoterStatusRow row in debtors)
        {
            if (row.AcceptedVotes == 0)
                sb.AppendLine("- " + row.VoterName + " - не голосовал(а)");
            else
                sb.AppendLine("- " + row.VoterName + " - нет оценок: " + row.MissingWorks);
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildStatus(VoterStatusRow row, int workCount)
    {
        if (!row.RequiredToVote)
            return row.AcceptedVotes > 0 ? "Голос есть, вне обязательных" : "Не обязателен";

        if (row.AcceptedVotes == 0)
            return "Не голосовал";

        if (workCount > 0 && row.MissingCount > 0)
            return "Неполное голосование";

        if (!string.IsNullOrWhiteSpace(row.UnknownWorks))
            return "Есть неизвестные номера";

        return "ОК";
    }

    private static string BuildNote(VoterStatusRow row, int workCount)
    {
        var parts = new List<string>();
        if (row.SelfVotes > 0)
            parts.Add("самоголосование будет 0");

        if (!string.IsNullOrWhiteSpace(row.UnknownWorks))
            parts.Add("неизвестные №: " + row.UnknownWorks);

        if (workCount == 0)
            parts.Add("список работ не задан");

        return string.Join("; ", parts);
    }

    private static string FormatNumbers(IEnumerable<int> numbers)
    {
        return string.Join(", ", numbers.Distinct().OrderBy(x => x).Select(x => x.ToString("00")));
    }
}
