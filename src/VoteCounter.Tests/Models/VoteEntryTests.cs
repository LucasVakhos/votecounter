namespace VoteCounter.Tests.Models;

public class VoteEntryTests
{
    [Fact]
    public void VoteEntry_WithValidData_SetsProperties()
    {
        // Arrange & Act
        var vote = new VoteEntry
        {
            VoterName = "John Doe",
            VoterKey = "johndoe",
            WorkNo = 5,
            Score = 4,
            ContestId = "contest-1"
        };

        // Assert
        vote.VoterName.Should().Be("John Doe");
        vote.VoterKey.Should().Be("johndoe");
        vote.WorkNo.Should().Be(5);
        vote.Score.Should().Be(4);
    }

    [Fact]
    public void VoteEntry_WithNoteAndRuleInfo_StoresData()
    {
        // Arrange & Act
        var vote = new VoteEntry
        {
            VoterName = "Voter",
            WorkNo = 1,
            Score = 5,
            RuleNote = "Clipped from 10 to 5",
            WasChangedByRules = true
        };

        // Assert
        vote.RuleNote.Should().Be("Clipped from 10 to 5");
        vote.WasChangedByRules.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void VoteEntry_WithDifferentScores_AcceptsThem(decimal score)
    {
        // Arrange & Act
        var vote = new VoteEntry { Score = score };

        // Assert
        vote.Score.Should().Be(score);
    }

    [Fact]
    public void VoteEntry_TracksMultipleScores()
    {
        // Arrange
        var vote = new VoteEntry 
        { 
            VoterName = "Voter",
            WorkNo = 1,
            Score = 5,
            VotedScore = 5,
            AcceptedScore = 4
        };

        // Assert
        vote.Score.Should().Be(5);
        vote.VotedScore.Should().Be(5);
        vote.AcceptedScore.Should().Be(4);
    }
}
