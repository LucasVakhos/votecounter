using VoteCounter.Core.Models;
using VoteCounter.Core.Services;

namespace VoteCounter.Web.Services;

/// <summary>
/// Сервис для обработки голосов в Blazor приложении
/// </summary>
public class VoteService
{
    private readonly VoteParser _voteParser;
    private readonly VoteRuleService _voteRuleService;
    private readonly ContestResultsService _resultsService;

    public VoteService(
        VoteParser voteParser,
        VoteRuleService voteRuleService,
        ContestResultsService resultsService)
    {
        _voteParser = voteParser;
        _voteRuleService = voteRuleService;
        _resultsService = resultsService;
    }

    /// <summary>
    /// Импортировать голоса из текста
    /// </summary>
    public async Task<ImportResult> ImportVotesAsync(string voteText, Contest contest)
    {
        var result = _voteParser.Parse(voteText, contest.Id);
        
        // Применить правила валидации
        _voteRuleService.Apply(contest, result);
        
        return await Task.FromResult(result);
    }

    /// <summary>
    /// Получить результаты голосования
    /// </summary>
    public async Task<ContestResultsReport?> GetResultsAsync(Contest contest, List<VoteEntry> votes)
    {
        var results = _resultsService.BuildReport(contest, votes);
        return await Task.FromResult(results);
    }
}
