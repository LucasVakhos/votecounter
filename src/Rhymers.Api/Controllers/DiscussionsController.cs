using Microsoft.AspNetCore.Mvc;
using Rhymers.Core.Models;
using Rhymers.Core.Services;
using Rhymers.Core.Data;

namespace Rhymers.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DiscussionsController : ControllerBase
{
    private readonly DiscussionService _discussionService;
    private readonly RhymersDbContext _context;
    private readonly PersistenceService _persistenceService;

    public DiscussionsController(
        DiscussionService discussionService,
        RhymersDbContext context,
        PersistenceService persistenceService)
    {
        _discussionService = discussionService;
        _context = context;
        _persistenceService = persistenceService;
    }

    /// <summary>
    /// Получить текущего пользователя из заголовка X-User-Name
    /// </summary>
    private async Task<User?> GetCurrentUserAsync()
    {
        if (HttpContext.Request.Headers.TryGetValue("X-User-Name", out var userName))
        {
            return await _persistenceService.GetUserByUsernameAsync(userName.ToString());
        }
        return null;
    }

    // ========== Contest Comments ==========

    /// <summary>
    /// Получить все комментарии к конкурсу
    /// </summary>
    /// <param name="contestId">ID конкурса</param>
    [HttpGet("contests/{contestId}/comments")]
    public async Task<ActionResult<List<ContestComment>>> GetContestComments(string contestId)
    {
        var comments = await _discussionService.GetContestCommentsAsync(contestId);
        return Ok(comments);
    }

    /// <summary>
    /// Добавить комментарий к конкурсу
    /// </summary>
    [HttpPost("contests/{contestId}/comments")]
    public async Task<ActionResult<ContestComment>> AddContestComment(
        string contestId,
        [FromBody] AddCommentRequest request)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
            return Unauthorized("Требуется авторизация");

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("Содержание комментария не может быть пустым");

        var comment = await _discussionService.AddContestCommentAsync(
            contestId,
            currentUser.DisplayName,
            currentUser.Role,
            request.Content,
            request.ParentCommentId);

        return Created($"/api/discussions/comments/{comment.Id}", comment);
    }

    /// <summary>
    /// Лайк на комментарий
    /// </summary>
    [HttpPost("comments/{commentId}/like")]
    public async Task<IActionResult> LikeComment(string commentId)
    {
        var result = await _discussionService.LikeContestCommentAsync(commentId);
        if (!result)
            return NotFound("Комментарий не найден");

        return Ok();
    }

    /// <summary>
    /// Одобрить комментарий (модератор)
    /// </summary>
    [HttpPost("comments/{commentId}/approve")]
    public async Task<IActionResult> ApproveComment(string commentId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser?.Role < UserRole.Moderator)
            return Forbid();

        var result = await _discussionService.ApproveContestCommentAsync(commentId, currentUser.DisplayName);
        if (!result)
            return NotFound("Комментарий не найден");

        return Ok();
    }

    /// <summary>
    /// Скрыть комментарий (модератор)
    /// </summary>
    [HttpPost("comments/{commentId}/hide")]
    public async Task<IActionResult> HideComment(string commentId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser?.Role < UserRole.Moderator)
            return Forbid();

        var result = await _discussionService.HideContestCommentAsync(commentId);
        if (!result)
            return NotFound("Комментарий не найден");

        return Ok();
    }

    // ========== Work Reviews ==========

    /// <summary>
    /// Получить все рецензии на конкретную работу
    /// </summary>
    [HttpGet("contests/{contestId}/works/{workNumber}/reviews")]
    public async Task<ActionResult<List<WorkReview>>> GetWorkReviews(string contestId, int workNumber)
    {
        var reviews = await _discussionService.GetWorkReviewsAsync(contestId, workNumber);
        return Ok(reviews);
    }

    /// <summary>
    /// Добавить рецензию на работу
    /// </summary>
    [HttpPost("contests/{contestId}/works/{workNumber}/reviews")]
    public async Task<ActionResult<WorkReview>> AddWorkReview(
        string contestId,
        int workNumber,
        [FromBody] AddReviewRequest request)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
            return Unauthorized("Требуется авторизация");

        if (currentUser.Role == UserRole.Reader)
            return Forbid("Только авторы и выше могут писать рецензии");

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("Заголовок и содержание рецензии не могут быть пусты");

        var review = await _discussionService.AddWorkReviewAsync(
            contestId,
            workNumber,
            request.WorkTitle ?? "",
            currentUser.DisplayName,
            currentUser.Role,
            request.Title,
            request.Content,
            request.Rating,
            request.Strengths,
            request.AreasForImprovement);

        return Created($"/api/discussions/reviews/{review.Id}", review);
    }

    /// <summary>
    /// Получить статистику рецензий для работы
    /// </summary>
    [HttpGet("contests/{contestId}/works/{workNumber}/review-stats")]
    public async Task<ActionResult<ReviewStatsResponse>> GetReviewStats(string contestId, int workNumber)
    {
        var (totalReviews, averageRating, topReviewsCount) = 
            await _discussionService.GetWorkReviewStatsAsync(contestId, workNumber);

        return Ok(new ReviewStatsResponse
        {
            TotalReviews = totalReviews,
            AverageRating = averageRating,
            TopReviewsCount = topReviewsCount
        });
    }

    /// <summary>
    /// Отметить рецензию как полезную
    /// </summary>
    [HttpPost("reviews/{reviewId}/helpful")]
    public async Task<IActionResult> MarkReviewAsHelpful(string reviewId)
    {
        var result = await _discussionService.MarkReviewAsHelpfulAsync(reviewId);
        if (!result)
            return NotFound("Рецензия не найдена");

        return Ok();
    }

    /// <summary>
    /// Одобрить рецензию (модератор)
    /// </summary>
    [HttpPost("reviews/{reviewId}/approve")]
    public async Task<IActionResult> ApproveReview(string reviewId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser?.Role < UserRole.Moderator)
            return Forbid();

        var result = await _discussionService.ApproveWorkReviewAsync(reviewId, currentUser.DisplayName);
        if (!result)
            return NotFound("Рецензия не найдена");

        return Ok();
    }

    /// <summary>
    /// Скрыть рецензию (модератор)
    /// </summary>
    [HttpPost("reviews/{reviewId}/hide")]
    public async Task<IActionResult> HideReview(string reviewId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser?.Role < UserRole.Moderator)
            return Forbid();

        var result = await _discussionService.HideWorkReviewAsync(reviewId);
        if (!result)
            return NotFound("Рецензия не найдена");

        return Ok();
    }

    /// <summary>
    /// Добавить ответ автора на рецензию
    /// </summary>
    [HttpPost("reviews/{reviewId}/author-response")]
    public async Task<IActionResult> AddAuthorResponse(
        string reviewId,
        [FromBody] AuthorResponseRequest request)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
            return Unauthorized("Требуется авторизация");

        if (string.IsNullOrWhiteSpace(request.Response))
            return BadRequest("Ответ не может быть пустым");

        var result = await _discussionService.AddAuthorResponseAsync(reviewId, request.Response);
        if (!result)
            return NotFound("Рецензия не найдена");

        return Ok();
    }
}

public class AddCommentRequest
{
    public string Content { get; set; } = string.Empty;
    public string? ParentCommentId { get; set; }
}

public class AddReviewRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int? Rating { get; set; }
    public string? Strengths { get; set; }
    public string? AreasForImprovement { get; set; }
    public string? WorkTitle { get; set; }
}

public class AuthorResponseRequest
{
    public string Response { get; set; } = string.Empty;
}

public class ReviewStatsResponse
{
    public int TotalReviews { get; set; }
    public decimal? AverageRating { get; set; }
    public int TopReviewsCount { get; set; }
}
