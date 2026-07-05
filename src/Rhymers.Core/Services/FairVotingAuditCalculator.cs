namespace Rhymers.Core.Services;

public static class FairVotingAuditCalculator
{
    public const string FairVotingSystemUserId = "system:fair-voting";
    public const string AdminAverageSystemUserId = "system:admin-average";

    public static List<FairVotingAuditRow> BuildRows(IReadOnlyCollection<WorkSubmission> submissions, IReadOnlyCollection<ContestVote> votes)
    {
        var result = new List<FairVotingAuditRow>();

        foreach (var item in submissions.Select((value, index) => new { value, index }))
        {
            var submission = item.value;
            var submissionVotes = votes.Where(v => v.SubmissionId == submission.Id).ToList();

            var humanVotes = submissionVotes
                .Where(v =>
                    !string.Equals(v.VoterUserId, FairVotingSystemUserId, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(v.VoterUserId, AdminAverageSystemUserId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var humanCount = humanVotes.Count;
            decimal? humanAverage = humanCount == 0 ? null : (decimal?)humanVotes.Average(v => v.Score);

            var fairVote = submissionVotes
                .Where(v => string.Equals(v.VoterUserId, FairVotingSystemUserId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(v => v.UpdatedAt)
                .FirstOrDefault();

            var adminVote = submissionVotes
                .Where(v => string.Equals(v.VoterUserId, AdminAverageSystemUserId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(v => v.UpdatedAt)
                .FirstOrDefault();

            decimal? fairScore = fairVote?.Score;
            decimal? adminScore = adminVote?.Score;
            decimal? fairDelta = (humanAverage.HasValue && fairScore.HasValue)
                ? fairScore.Value - humanAverage.Value
                : null;
            decimal? adminDelta = (humanAverage.HasValue && adminScore.HasValue)
                ? adminScore.Value - humanAverage.Value
                : null;

            result.Add(new FairVotingAuditRow
            {
                DisplayNo = submission.Work.Number > 0 ? submission.Work.Number : item.index + 1,
                Topic = string.IsNullOrWhiteSpace(submission.Work.Topic) ? "Без темы" : submission.Work.Topic,
                Author = submission.Work.Author,
                HumanVotesCount = humanCount,
                HumanAverage = humanAverage,
                FairSystemScore = fairScore,
                AdminSystemScore = adminScore,
                FairDelta = fairDelta,
                AdminDelta = adminDelta,
                FairSystemUpdatedAt = fairVote?.UpdatedAt,
                AdminSystemUpdatedAt = adminVote?.UpdatedAt,
                RuleLabel = BuildRuleLabel(fairVote != null, adminVote != null)
            });
        }

        return result
            .OrderBy(r => r.DisplayNo)
            .ToList();
    }

    public static List<FairVotingAuditRow> FilterByDeviation(IEnumerable<FairVotingAuditRow> rows, decimal threshold, bool onlySignificantDeviations)
    {
        if (!onlySignificantDeviations)
            return rows.ToList();

        var normalizedThreshold = threshold < 0 ? 0 : threshold;
        return rows
            .Where(r =>
                (r.FairDelta.HasValue && Math.Abs(r.FairDelta.Value) >= normalizedThreshold) ||
                (r.AdminDelta.HasValue && Math.Abs(r.AdminDelta.Value) >= normalizedThreshold))
            .ToList();
    }

    public static string BuildRuleLabel(bool hasFairVote, bool hasAdminVote)
    {
        if (hasFairVote && hasAdminVote)
            return "Честный бот + админ-авто по среднему";
        if (hasFairVote)
            return "Честный бот по среднему голосов";
        if (hasAdminVote)
            return "Админ-авто по среднему при закрытии";

        return "Только человеческие оценки";
    }
}
