namespace VoteCounter.Tests.Models;

public class ContestTests
{
    [Fact]
    public void Contest_NewInstance_HasValidId()
    {
        // Arrange & Act
        var contest = new Contest();

        // Assert
        contest.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Contest_NewInstance_HasEmptyCollections()
    {
        // Arrange & Act
        var contest = new Contest();

        // Assert
        contest.Works.Should().NotBeNull().And.BeEmpty();
        contest.Voters.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Contest_WithValidData_SetsAllProperties()
    {
        // Arrange & Act
        var contest = new Contest
        {
            Number = "001",
            Name = "Spring Contest 2026",
            BaseVote = 3,
            MaxVote = 5,
            LimitMaxVote = 2,
            AllowZeroVotes = true,
            TreatSelfVoteAsZero = false,
            HostKnowsAuthors = true
        };

        // Assert
        contest.Number.Should().Be("001");
        contest.Name.Should().Be("Spring Contest 2026");
        contest.BaseVote.Should().Be(3);
        contest.MaxVote.Should().Be(5);
        contest.AllowZeroVotes.Should().BeTrue();
    }

    [Fact]
    public void Contest_CanAddWorks()
    {
        // Arrange
        var contest = new Contest();
        var work = new ContestWork 
        { 
            Number = 1, 
            Title = "Beautiful Day", 
            Author = "John Poet",
            Content = "Some poem text..."
        };

        // Act
        contest.Works.Add(work);

        // Assert
        contest.Works.Should().HaveCount(1);
        contest.Works[0].Title.Should().Be("Beautiful Day");
    }

    [Fact]
    public void Contest_CanAddVoters()
    {
        // Arrange
        var contest = new Contest();
        var voter = new VoterSetting 
        { 
            Name = "Judge One",
            MustVote = true,
            HasVoted = false
        };

        // Act
        contest.Voters.Add(voter);

        // Assert
        contest.Voters.Should().HaveCount(1);
        contest.Voters[0].Name.Should().Be("Judge One");
    }

    [Fact]
    public void Contest_Timestamps_AreSet()
    {
        // Arrange & Act
        var beforeCreation = DateTime.Now;
        var contest = new Contest();
        var afterCreation = DateTime.Now;

        // Assert
        contest.CreatedAt.Should().BeOnOrAfter(beforeCreation);
        contest.UpdatedAt.Should().BeOnOrAfter(beforeCreation);
    }
}
