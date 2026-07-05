namespace VoteCounter.Tests.Services;

public class VoteParserTests
{
    private readonly VoteParser _parser = new();

    [Fact]
    public void Parse_WithValidVoteBlock_ReturnsImportResult()
    {
        // Arrange
        var voteText = @"Иван Петров
01-3
02-4";
        var contestId = "test-contest";

        // Act
        var result = _parser.Parse(voteText, contestId);

        // Assert
        result.Should().NotBeNull();
        result.Blocks.Should().NotBeNull();
    }

    [Fact]
    public void Parse_WithEmptyText_ReturnsEmptyResult()
    {
        // Arrange & Act
        var result = _parser.Parse("", "contest-id");

        // Assert
        result.Should().NotBeNull();
        result.VoteCount.Should().Be(0);
    }

    [Fact]
    public void Parse_WithValidText_HasBlocks()
    {
        // Arrange
        var voteText = @"Voter Name
01-5
02-4";

        // Act
        var result = _parser.Parse(voteText, "contest-id");

        // Assert
        result.Should().NotBeNull();
        result.Blocks.Should().NotBeNull();
    }

    [Fact]
    public void Parse_WithContestParameter_ProcessesCorrectly()
    {
        // Arrange
        var voteText = "Voter\n01-5";
        var contest = new Contest { Id = "c1", Number = "001" };
        contest.Works.Add(new ContestWork { Number = 1, Title = "W1" });

        // Act
        var result = _parser.Parse(voteText, contest.Id, contest);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void Parse_WithMultipleLines_ParsesSuccessfully()
    {
        // Arrange
        var voteText = @"Voter 1
01-5
02-4";

        // Act
        var result = _parser.Parse(voteText, "contest");

        // Assert
        result.Should().NotBeNull();
        result.Blocks.Count.Should().BeGreaterThan(0);
    }
}
