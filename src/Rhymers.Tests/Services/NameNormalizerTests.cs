namespace Rhymers.Tests.Services;

public class NameNormalizerTests
{
    [Fact]
    public void Normalize_WithEmptyString_ReturnsEmpty()
    {
        // Arrange & Act
        var result = NameNormalizer.Normalize("");

        // Assert
        result.Should().Be("");
    }

    [Fact]
    public void Normalize_WithNullString_ReturnsEmpty()
    {
        // Arrange & Act
        var result = NameNormalizer.Normalize(null);

        // Assert
        result.Should().Be("");
    }

    [Fact]
    public void Normalize_WithWhitespace_ReturnsEmpty()
    {
        // Arrange & Act
        var result = NameNormalizer.Normalize("   \t  \n  ");

        // Assert
        result.Should().Be("");
    }

    [Theory]
    [InlineData("John Doe", "johndoe")]
    [InlineData("JOHN DOE", "johndoe")]
    [InlineData("  John  Doe  ", "johndoe")]
    public void Normalize_WithValidNames_ReturnsNormalized(string input, string expected)
    {
        // Arrange & Act
        var result = NameNormalizer.Normalize(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Normalize_WithRussianCharacters_HandlesCorrectly()
    {
        // Arrange & Act
        var result = NameNormalizer.Normalize("Иван Петров");

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Normalize_RemovesSpaces()
    {
        // Arrange & Act
        var result = NameNormalizer.Normalize("John Smith");

        // Assert
        result.Should().NotContain(" ");
    }
}
