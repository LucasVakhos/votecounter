using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Rhymers.Core.Models;
using Rhymers.Core.Services;

namespace Rhymers.Tests;

/// <summary>
/// Performance benchmarks for VoteRuleService.
/// Validates rule application performance with pre-computed topic keys.
/// </summary>
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class VoteRuleServiceBenchmarks
{
    private VoteRuleService _ruleService = null!;
    private Contest _contest = null!;
    private ImportResult _importResult = null!;

    [GlobalSetup]
    public void Setup()
    {
        _ruleService = new VoteRuleService();
        _contest = new Contest
        {
            Id = "test-contest",
            Name = "Test Contest",
            BaseVote = 3,
            MaxVote = 4,
            VoteLimit = 0,
            TreatSelfVoteAsZero = true
        };

        // Setup 50 works with different topics
        for (int i = 1; i <= 50; i++)
        {
            _contest.Works.Add(new ContestWork
            {
                Number = i,
                Title = $"Work {i}",
                Author = $"Author {i}",
                Topic = $"Topic {(i % 5) + 1}"
            });
        }

        // Create 200 votes from 20 voters
        _importResult = GenerateImportResult(20, 10);
    }

    private ImportResult GenerateImportResult(int voters, int votesPerVoter)
    {
        var result = new ImportResult();
        for (int v = 0; v < voters; v++)
        {
            var block = new ParsedVoteBlock { VoterName = $"Voter {v}" };
            for (int i = 1; i <= votesPerVoter; i++)
            {
                block.Votes.Add(new VoteEntry
                {
                    ContestId = _contest.Id,
                    VoterName = $"Voter {v}",
                    VoterKey = $"voter{v}",
                    WorkNo = (i % 50) + 1,
                    Score = (i % 3) + 2,
                    ScoreText = $"{(i % 3) + 2}"
                });
            }
            result.Blocks.Add(block);
        }
        return result;
    }

    [Benchmark(Description = "Apply rules to 200 votes")]
    public void ApplyRules()
    {
        // Create a fresh copy for each iteration
        var testResult = GenerateImportResult(20, 10);
        _ruleService.Apply(_contest, testResult);
    }

    [Benchmark(Description = "Apply rules with self-vote checks")]
    public void ApplyRulesWithSelfVotes()
    {
        _contest.TreatSelfVoteAsZero = true;
        var testResult = GenerateImportResult(20, 10);
        _ruleService.Apply(_contest, testResult);
    }

    [Benchmark(Description = "Apply rules with vote limits")]
    public void ApplyRulesWithLimits()
    {
        _contest.VoteLimit = 5;
        var testResult = GenerateImportResult(20, 10);
        _ruleService.Apply(_contest, testResult);
    }
}

/// <summary>
/// Performance benchmarks for ContestResultsService.
/// Validates results generation performance.
/// </summary>
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class ContestResultsServiceBenchmarks
{
    private ContestResultsService _resultsService = null!;
    private Contest _contest = null!;
    private List<VoteEntry> _votes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _resultsService = new ContestResultsService();
        _contest = new Contest
        {
            Id = "test-contest",
            Name = "Test Contest",
            BaseVote = 3,
            MaxVote = 4
        };

        // Setup 50 works
        for (int i = 1; i <= 50; i++)
        {
            _contest.Works.Add(new ContestWork
            {
                Number = i,
                Title = $"Work {i}: Title",
                Author = $"Author Name {i}",
                Topic = $"Topic {(i % 5) + 1}"
            });
        }

        // Generate 500 votes from 50 voters
        _votes = GenerateVotes(50, 10);
    }

    private List<VoteEntry> GenerateVotes(int voters, int votesPerVoter)
    {
        var votes = new List<VoteEntry>();
        for (int v = 0; v < voters; v++)
        {
            for (int i = 1; i <= votesPerVoter; i++)
            {
                votes.Add(new VoteEntry
                {
                    ContestId = _contest.Id,
                    VoterName = $"Voter Name {v}",
                    VoterKey = $"votername{v}",
                    WorkNo = (i % 50) + 1,
                    Score = (i % 3) + 2,
                    ScoreText = $"{(i % 3) + 2}",
                    AcceptedScore = (i % 3) + 2
                });
            }
        }
        return votes;
    }

    [Benchmark(Description = "Build results report (500 votes)")]
    public ContestResultsReport BuildReport()
    {
        return _resultsService.BuildReport(_contest, _votes);
    }

    [Benchmark(Description = "Generate final text output")]
    public string BuildFinalText()
    {
        var report = _resultsService.BuildReport(_contest, _votes);
        return _resultsService.BuildFinalText(_contest, report);
    }

    [Benchmark(Description = "Generate winners text output")]
    public string BuildWinnersText()
    {
        var report = _resultsService.BuildReport(_contest, _votes);
        return _resultsService.BuildWinnersText(report);
    }
}
