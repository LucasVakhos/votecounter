using Microsoft.AspNetCore.Mvc;
using Rhymers.Core.Models;
using Rhymers.Core.Services;

namespace Rhymers.Api.Controllers;

/// <summary>
/// API endpoints for vote management and processing
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class VotesController : ControllerBase
{
    private readonly ILogger<VotesController> _logger;
    private readonly VoteParser _voteParser;
    private readonly VoteRuleService _voteRuleService;
    private readonly ContestResultsService _resultsService;
    private static readonly Dictionary<string, List<VoteEntry>> _votesByContest = new();

    public VotesController(
        ILogger<VotesController> logger,
        VoteParser voteParser,
        VoteRuleService voteRuleService,
        ContestResultsService resultsService)
    {
        _logger = logger;
        _voteParser = voteParser;
        _voteRuleService = voteRuleService;
        _resultsService = resultsService;
    }

    /// <summary>
    /// Import votes from text format
    /// </summary>
    [HttpPost("import")]
    public ActionResult<ImportResult> ImportVotes([FromBody] ImportVotesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.VoteText))
        {
            return BadRequest("Vote text is required");
        }

        if (string.IsNullOrWhiteSpace(request.ContestId))
        {
            return BadRequest("Contest ID is required");
        }

        try
        {
            var result = _voteParser.Parse(request.VoteText, request.ContestId);
            _logger.LogInformation("Votes imported for contest {ContestId}. Blocks: {BlockCount}", 
                request.ContestId, result.Blocks?.Count ?? 0);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing votes for contest {ContestId}", request.ContestId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Validate votes against contest rules
    /// </summary>
    [HttpPost("validate")]
    public ActionResult<ImportResult> ValidateVotes([FromBody] ValidateVotesRequest request)
    {
        if (request.ImportResult == null)
        {
            return BadRequest("Import result is required");
        }

        if (string.IsNullOrWhiteSpace(request.ContestId))
        {
            return BadRequest("Contest ID is required");
        }

        try
        {
            // Create minimal contest for validation
            var contest = new Contest
            {
                Id = request.ContestId,
                Name = "Validation Contest"
            };

            _voteRuleService.Apply(contest, request.ImportResult);
            _logger.LogInformation("Votes validated for contest {ContestId}", request.ContestId);

            return Ok(request.ImportResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating votes for contest {ContestId}", request.ContestId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get votes for a contest
    /// </summary>
    [HttpGet("contest/{contestId}")]
    public ActionResult<IEnumerable<VoteEntry>> GetContestVotes(string contestId)
    {
        if (!_votesByContest.TryGetValue(contestId, out var votes))
        {
            _logger.LogWarning("No votes found for contest {ContestId}", contestId);
            return Ok(new List<VoteEntry>());
        }

        return Ok(votes);
    }

    /// <summary>
    /// Generate results for a contest
    /// </summary>
    [HttpPost("results")]
    public ActionResult<ContestResultsReport> GetResults([FromBody] GetResultsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ContestId))
        {
            return BadRequest("Contest ID is required");
        }

        try
        {
            // Get votes for this contest
            if (!_votesByContest.TryGetValue(request.ContestId, out var votes))
            {
                votes = new List<VoteEntry>();
            }

            // Create minimal contest for report generation
            var contest = new Contest
            {
                Id = request.ContestId,
                Name = "Results Contest"
            };

            var report = _resultsService.BuildReport(contest, votes);

            _logger.LogInformation("Results generated for contest {ContestId}", request.ContestId);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating results for contest {ContestId}", request.ContestId);
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record ImportVotesRequest(string ContestId, string VoteText);
public record ValidateVotesRequest(string ContestId, ImportResult ImportResult);
public record GetResultsRequest(string ContestId);
