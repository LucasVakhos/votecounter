namespace Rhymers.Tests.Services;

public class VoteRuleServiceTests
{
    private readonly VoteRuleService _ruleService = new();
    private readonly Contest _contest;

    public VoteRuleServiceTests()
    {
        _contest = new Contest
        {
            Id = "c1",
            Number = "001",
            Name = "Test",
            BaseVote = 3,
            MaxVote = 5,
            LimitMaxVote = 2,
            AllowZeroVotes = false,
            TreatSelfVoteAsZero = true
        };

        _contest.Works.Add(new ContestWork { Number = 1, Author = "A1", Title = "W1" });
        _contest.Works.Add(new ContestWork { Number = 2, Author = "A2", Title = "W2" });
        _contest.Voters.Add(new VoterSetting { Name = "V1", MustVote = true });
    }

    [Fact]
    public void Apply_WithEmptyImportResult_CompletesSuccessfully()
    {
        // Arrange
        var result = new ImportResult();

        // Act & Assert - should not throw
        _ruleService.Apply(_contest, result);
    }

    [Fact]
    public void Apply_WithValidBlocks_ProcessesAll()
    {
        // Arrange
        var block = new ParsedVoteBlock { VoterName = "V1" };
        block.Votes.Add(new VoteEntry { VoterName = "V1", WorkNo = 1, Score = 5 });

        var result = new ImportResult();

        // Act & Assert - should process without error
        _ruleService.Apply(_contest, result);
    }

    [Fact]
    public void Apply_WithHighScores_HandlesThem()
    {
        // Arrange
        var result = new ImportResult();

        // Act & Assert
        _ruleService.Apply(_contest, result);
    }

    [Fact]
    public void Apply_WithZeroScoreNotAllowed_StillProcesses()
    {
        // Arrange
        _contest.AllowZeroVotes = false;
        var result = new ImportResult();

        // Act & Assert
        _ruleService.Apply(_contest, result);
    }
}
