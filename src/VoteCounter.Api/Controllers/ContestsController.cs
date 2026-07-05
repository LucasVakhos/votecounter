using Microsoft.AspNetCore.Mvc;
using VoteCounter.Core.Models;
using VoteCounter.Core.Services;

namespace VoteCounter.Api.Controllers;

/// <summary>
/// API endpoints for contest management
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ContestsController : ControllerBase
{
    private readonly ILogger<ContestsController> _logger;
    private static readonly List<Contest> _contests = new();

    public ContestsController(ILogger<ContestsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get all contests
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<Contest>> GetAll()
    {
        _logger.LogInformation("Getting all contests. Total: {Count}", _contests.Count);
        return Ok(_contests);
    }

    /// <summary>
    /// Get contest by ID
    /// </summary>
    [HttpGet("{id}")]
    public ActionResult<Contest> GetById(string id)
    {
        var contest = _contests.FirstOrDefault(c => c.Id == id);
        if (contest == null)
        {
            _logger.LogWarning("Contest not found: {ContestId}", id);
            return NotFound($"Contest with ID {id} not found");
        }
        return Ok(contest);
    }

    /// <summary>
    /// Create a new contest
    /// </summary>
    [HttpPost]
    public ActionResult<Contest> Create([FromBody] CreateContestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Contest name is required");
        }

        var contest = new Contest
        {
            Id = Guid.NewGuid().ToString("N"),
            Number = (_contests.Count + 1).ToString("000"),
            Name = request.Name,
            HostName = request.HostName ?? "Unknown",
            StartedAt = DateTime.UtcNow
        };

        _contests.Add(contest);
        _logger.LogInformation("Contest created: {ContestId} - {ContestName}", contest.Id, contest.Name);

        return CreatedAtAction(nameof(GetById), new { id = contest.Id }, contest);
    }

    /// <summary>
    /// Update contest
    /// </summary>
    [HttpPut("{id}")]
    public ActionResult Update(string id, [FromBody] UpdateContestRequest request)
    {
        var contest = _contests.FirstOrDefault(c => c.Id == id);
        if (contest == null)
        {
            return NotFound($"Contest with ID {id} not found");
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
            contest.Name = request.Name;
        if (!string.IsNullOrWhiteSpace(request.HostName))
            contest.HostName = request.HostName;

        _logger.LogInformation("Contest updated: {ContestId}", id);
        return Ok(contest);
    }

    /// <summary>
    /// Add work to contest
    /// </summary>
    [HttpPost("{id}/works")]
    public ActionResult AddWork(string id, [FromBody] ContestWork work)
    {
        var contest = _contests.FirstOrDefault(c => c.Id == id);
        if (contest == null)
        {
            return NotFound($"Contest with ID {id} not found");
        }

        contest.Works.Add(work);
        _logger.LogInformation("Work added to contest {ContestId}: {WorkNumber}", id, work.Number);

        return Ok(contest);
    }

    /// <summary>
    /// Get contest works
    /// </summary>
    [HttpGet("{id}/works")]
    public ActionResult<IEnumerable<ContestWork>> GetWorks(string id)
    {
        var contest = _contests.FirstOrDefault(c => c.Id == id);
        if (contest == null)
        {
            return NotFound($"Contest with ID {id} not found");
        }

        return Ok(contest.Works);
    }
}

public record CreateContestRequest(string Name, string? HostName = null);
public record UpdateContestRequest(string? Name = null, string? HostName = null);
