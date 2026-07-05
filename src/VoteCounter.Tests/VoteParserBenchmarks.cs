using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using VoteCounter.Core.Models;
using VoteCounter.Core.Services;

namespace VoteCounter.Tests;

/// <summary>
/// Performance benchmarks for VoteParser service.
/// Validates parsing performance improvements from NameNormalizer caching.
/// </summary>
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class VoteParserBenchmarks
{
    private VoteParser _parser = null!;
    private string _simpleVoteText = null!;
    private string _complexVoteText = null!;
    private Contest _contest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _parser = new VoteParser();
        _contest = new Contest { Id = "test-contest", Name = "Test Contest" };

        // Add test works
        for (int i = 1; i <= 10; i++)
        {
            _contest.Works.Add(new ContestWork
            {
                Number = i,
                Title = $"Work {i}",
                Author = $"Author {i}",
                Topic = $"Topic {i % 3 + 1}"
            });
        }

        // Simple vote text (5 voters, 50 votes)
        _simpleVoteText = GenerateVoteText(5, 10);

        // Complex vote text (50 voters, 500 votes)
        _complexVoteText = GenerateVoteText(50, 10);
    }

    private string GenerateVoteText(int voters, int votesPerVoter)
    {
        var lines = new List<string>();
        for (int v = 0; v < voters; v++)
        {
            lines.Add($"Voter Name {v}");
            for (int i = 1; i <= votesPerVoter; i++)
            {
                int workNo = (i % 10) + 1;
                int score = (i % 3) + 2; // 2-4
                lines.Add($"{workNo:00} {score}");
            }
            lines.Add("---");
        }
        return string.Join("\n", lines);
    }

    [Benchmark(Description = "Parse simple votes (5 voters)")]
    public ImportResult ParseSimpleVotes()
    {
        return _parser.Parse(_simpleVoteText, "test-contest", _contest);
    }

    [Benchmark(Description = "Parse complex votes (50 voters)")]
    public ImportResult ParseComplexVotes()
    {
        return _parser.Parse(_complexVoteText, "test-contest", _contest);
    }

    [Benchmark(Description = "Parse and apply rules")]
    public ImportResult ParseAndApplyRules()
    {
        var result = _parser.Parse(_complexVoteText, "test-contest", _contest);
        return result;
    }
}
