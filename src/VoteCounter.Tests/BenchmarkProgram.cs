using BenchmarkDotNet.Running;
using VoteCounter.Tests;

// Run performance benchmarks
var summary = BenchmarkRunner.Run(new[]
{
    typeof(NameNormalizerBenchmarks),
    typeof(VoteParserBenchmarks),
    typeof(VoteRuleServiceBenchmarks),
    typeof(ContestResultsServiceBenchmarks)
});
