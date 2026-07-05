using VoteCounter.Core.Services;

namespace VoteCounter.Core.Models;

public sealed class AuthorDisclosureResult
{
    public Dictionary<int, string> AuthorsByWorkNo { get; } = new();
    public List<string> Warnings { get; } = new();
    public int ExpectedAuthorCount { get; set; }
    public int ExpectedWorkCount { get; set; }
    public int DuplicateWorkNumbers { get; set; }
    public int EmptyAuthorLines { get; set; }

    public int WorkCount => AuthorsByWorkNo.Count;
    public int AuthorCount => AuthorsByWorkNo.Values
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => NameNormalizer.Normalize(x))
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
}
