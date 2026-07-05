namespace Rhymers.Tests.Services;

public class FairVotingAuditCalculatorTests
{
    [Fact]
    public void BuildRows_ComputesFairAndAdminDeltas_UsingLatestSystemVotes()
    {
        var submittedAt = new DateTime(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc);
        var submissions = new List<WorkSubmission>
        {
            new()
            {
                Id = "s1",
                Work = new ContestWork
                {
                    Number = 2,
                    Topic = "Тема",
                    Author = "Автор"
                }
            }
        };

        var votes = new List<ContestVote>
        {
            new() { SubmissionId = "s1", VoterUserId = "u1", Score = 2, UpdatedAt = submittedAt.AddMinutes(1) },
            new() { SubmissionId = "s1", VoterUserId = "u2", Score = 4, UpdatedAt = submittedAt.AddMinutes(2) },
            new() { SubmissionId = "s1", VoterUserId = FairVotingAuditCalculator.FairVotingSystemUserId, Score = 3, UpdatedAt = submittedAt.AddMinutes(3) },
            new() { SubmissionId = "s1", VoterUserId = FairVotingAuditCalculator.FairVotingSystemUserId, Score = 5, UpdatedAt = submittedAt.AddMinutes(4) },
            new() { SubmissionId = "s1", VoterUserId = FairVotingAuditCalculator.AdminAverageSystemUserId, Score = 1, UpdatedAt = submittedAt.AddMinutes(5) },
            new() { SubmissionId = "s1", VoterUserId = FairVotingAuditCalculator.AdminAverageSystemUserId, Score = 2, UpdatedAt = submittedAt.AddMinutes(6) }
        };

        var rows = FairVotingAuditCalculator.BuildRows(submissions, votes);

        rows.Should().ContainSingle();
        var row = rows[0];
        row.DisplayNo.Should().Be(2);
        row.Topic.Should().Be("Тема");
        row.Author.Should().Be("Автор");
        row.HumanVotesCount.Should().Be(2);
        row.HumanAverage.Should().Be(3m);
        row.FairSystemScore.Should().Be(5m);
        row.AdminSystemScore.Should().Be(2m);
        row.FairDelta.Should().Be(2m);
        row.AdminDelta.Should().Be(-1m);
        row.FairSystemUpdatedAt.Should().Be(submittedAt.AddMinutes(4));
        row.AdminSystemUpdatedAt.Should().Be(submittedAt.AddMinutes(6));
        row.RuleLabel.Should().Be("Честный бот + админ-авто по среднему");
    }

    [Fact]
    public void BuildRows_WithOnlyHumanVotes_ProducesHumanOnlyRule()
    {
        var submissions = new List<WorkSubmission>
        {
            new()
            {
                Id = "s1",
                Work = new ContestWork
                {
                    Number = 0,
                    Topic = "",
                    Author = ""
                }
            }
        };

        var votes = new List<ContestVote>
        {
            new() { SubmissionId = "s1", VoterUserId = "u1", Score = 4 },
            new() { SubmissionId = "s1", VoterUserId = "u2", Score = 2 }
        };

        var rows = FairVotingAuditCalculator.BuildRows(submissions, votes);

        rows.Should().ContainSingle();
        var row = rows[0];
        row.DisplayNo.Should().Be(1);
        row.Topic.Should().Be("Без темы");
        row.Author.Should().Be("");
        row.HumanAverage.Should().Be(3m);
        row.FairSystemScore.Should().BeNull();
        row.AdminSystemScore.Should().BeNull();
        row.FairDelta.Should().BeNull();
        row.AdminDelta.Should().BeNull();
        row.RuleLabel.Should().Be("Только человеческие оценки");
    }

    [Fact]
    public void FilterByDeviation_WhenSignificantFilterOff_ReturnsAllRows()
    {
        var rows = new List<FairVotingAuditRow>
        {
            new() { DisplayNo = 1, FairDelta = 0.1m },
            new() { DisplayNo = 2, AdminDelta = 0.2m }
        };

        var visible = FairVotingAuditCalculator.FilterByDeviation(rows, 0.5m, false);

        visible.Should().HaveCount(2);
    }

    [Fact]
    public void FilterByDeviation_WhenSignificantFilterOn_UsesEitherDelta_AndNormalizesNegativeThreshold()
    {
        var rows = new List<FairVotingAuditRow>
        {
            new() { DisplayNo = 1, FairDelta = 0.2m, AdminDelta = null },
            new() { DisplayNo = 2, FairDelta = 0.1m, AdminDelta = 0.7m },
            new() { DisplayNo = 3, FairDelta = null, AdminDelta = null }
        };

        var allBecauseZeroThreshold = FairVotingAuditCalculator.FilterByDeviation(rows, -1m, true);
        allBecauseZeroThreshold.Select(r => r.DisplayNo).Should().Equal(1, 2);

        var onlyLarge = FairVotingAuditCalculator.FilterByDeviation(rows, 0.5m, true);
        onlyLarge.Select(r => r.DisplayNo).Should().Equal(2);
    }
}
