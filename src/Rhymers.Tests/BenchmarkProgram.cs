using BenchmarkDotNet.Running;
using Rhymers.Tests;

namespace Rhymers.Tests;

public static class BenchmarkProgram
{
    public static void RunAll()
    {
        BenchmarkRunner.Run(new[]
        {
            typeof(NameNormalizerBenchmarks),
            typeof(VoteParserBenchmarks),
            typeof(VoteRuleServiceBenchmarks),
            typeof(ContestResultsServiceBenchmarks)
        });
    }
}
