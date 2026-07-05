using Rhymers.Core.Data;
using Rhymers.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Rhymers.Core.Services;

/// <summary>
/// Сервис для управления дискуссиями, комментариями и рецензиями.
/// </summary>
public sealed class DiscussionService
{
    private readonly RhymersDbContext _context;
    private readonly RoleAuthorizationService _authService;

    public DiscussionService(RhymersDbContext context, RoleAuthorizationService authService)
    {
        _context = context;
        _authService = authService;
    }

    /// <summary>
    /// Добавить комментарий к конкурсу.
    /// </summary>
    public async Task<ContestComment> AddContestCommentAsync(
        string contestId,
        string authorName,
        UserRole authorRole,
        string content,
        string? parentCommentId = null)
    {
        var comment = new ContestComment
        {
            Id = Guid.NewGuid().ToString("N"),
            ContestId = contestId,
            AuthorName = authorName,
            AuthorRole = authorRole,
            Content = content,
            ParentCommentId = parentCommentId,
            IsApproved = authorRole >= UserRole.Moderator, // Автоматически одобрены для модераторов и админов
            CreatedAt = DateTime.UtcNow
        };

        _context.ContestComments.Add(comment);
        await _context.SaveChangesAsync();
        return comment;
    }

    /// <summary>
    /// Получить все одобренные комментарии к конкурсу (или все для модератора).
    /// </summary>
    public async Task<List<ContestComment>> GetContestCommentsAsync(
        string contestId,
        bool includeUnapproved = false,
        bool includeHidden = false)
    {
        var query = _context.ContestComments
            .Where(c => c.ContestId == contestId);

        if (!includeHidden)
            query = query.Where(c => !c.IsHidden);

        if (!includeUnapproved)
            query = query.Where(c => c.IsApproved);

        return await query
            .OrderBy(c => c.ParentCommentId) // Дочерние комментарии после родительских
            .ThenBy(c => c.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Получить ответы на конкретный комментарий.
    /// </summary>
    public async Task<List<ContestComment>> GetCommentRepliesAsync(string parentCommentId)
    {
        return await _context.ContestComments
            .Where(c => c.ParentCommentId == parentCommentId && !c.IsHidden)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Одобрить комментарий (модератор только).
    /// </summary>
    public async Task<bool> ApproveContestCommentAsync(string commentId, string moderatorName)
    {
        var comment = await _context.ContestComments.FindAsync(commentId);
        if (comment == null)
            return false;

        comment.IsApproved = true;
        comment.ApprovedAt = DateTime.UtcNow;
        comment.ApprovedBy = moderatorName;

        _context.ContestComments.Update(comment);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Скрыть комментарий (удалить от публики, но сохранить в БД).
    /// </summary>
    public async Task<bool> HideContestCommentAsync(string commentId)
    {
        var comment = await _context.ContestComments.FindAsync(commentId);
        if (comment == null)
            return false;

        comment.IsHidden = true;

        // Скрыть все дочерние комментарии
        var replies = await _context.ContestComments
            .Where(c => c.ParentCommentId == commentId)
            .ToListAsync();

        foreach (var reply in replies)
        {
            reply.IsHidden = true;
        }

        _context.Update(comment);
        _context.UpdateRange(replies);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Добавить лайк к комментарию.
    /// </summary>
    public async Task<bool> LikeContestCommentAsync(string commentId)
    {
        var comment = await _context.ContestComments.FindAsync(commentId);
        if (comment == null)
            return false;

        comment.LikeCount++;
        _context.ContestComments.Update(comment);
        await _context.SaveChangesAsync();
        return true;
    }

    // ========== WorkReview методы ==========

    /// <summary>
    /// Добавить рецензию на работу.
    /// </summary>
    public async Task<WorkReview> AddWorkReviewAsync(
        string contestId,
        int workNumber,
        string workTitle,
        string reviewerName,
        UserRole reviewerRole,
        string title,
        string content,
        int? rating = null,
        string? strengths = null,
        string? areasForImprovement = null)
    {
        var review = new WorkReview
        {
            Id = Guid.NewGuid().ToString("N"),
            ContestId = contestId,
            WorkNumber = workNumber,
            WorkTitle = workTitle,
            ReviewerName = reviewerName,
            ReviewerRole = reviewerRole,
            Title = title,
            Content = content,
            Rating = rating,
            Strengths = strengths,
            AreasForImprovement = areasForImprovement,
            IsApproved = reviewerRole >= UserRole.Moderator, // Автоматически для модераторов
            CreatedAt = DateTime.UtcNow
        };

        _context.WorkReviews.Add(review);
        await _context.SaveChangesAsync();
        return review;
    }

    /// <summary>
    /// Получить все одобренные рецензии на конкретную работу.
    /// </summary>
    public async Task<List<WorkReview>> GetWorkReviewsAsync(
        string contestId,
        int workNumber,
        bool includeUnapproved = false)
    {
        var query = _context.WorkReviews
            .Where(r => r.ContestId == contestId && r.WorkNumber == workNumber && !r.IsHidden);

        if (!includeUnapproved)
            query = query.Where(r => r.IsApproved && r.IsPublic);

        return await query
            .OrderByDescending(r => r.HelpfulCount)
            .ThenByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Получить все рецензии для конкурса (для модератора).
    /// </summary>
    public async Task<List<WorkReview>> GetContestReviewsAsync(
        string contestId,
        bool includeHidden = false,
        bool onlyUnapproved = false)
    {
        var query = _context.WorkReviews.Where(r => r.ContestId == contestId);

        if (!includeHidden)
            query = query.Where(r => !r.IsHidden);

        if (onlyUnapproved)
            query = query.Where(r => !r.IsApproved);

        return await query
            .OrderBy(r => r.WorkNumber)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Одобрить рецензию (сделать её видимой публике).
    /// </summary>
    public async Task<bool> ApproveWorkReviewAsync(string reviewId, string moderatorName)
    {
        var review = await _context.WorkReviews.FindAsync(reviewId);
        if (review == null)
            return false;

        review.IsApproved = true;
        review.IsPublic = true;
        review.ApprovedAt = DateTime.UtcNow;
        review.ApprovedBy = moderatorName;

        _context.WorkReviews.Update(review);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Скрыть рецензию (удалить от публики).
    /// </summary>
    public async Task<bool> HideWorkReviewAsync(string reviewId)
    {
        var review = await _context.WorkReviews.FindAsync(reviewId);
        if (review == null)
            return false;

        review.IsHidden = true;
        review.IsPublic = false;

        _context.WorkReviews.Update(review);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Добавить ответ автора на рецензию.
    /// </summary>
    public async Task<bool> AddAuthorResponseAsync(string reviewId, string response)
    {
        var review = await _context.WorkReviews.FindAsync(reviewId);
        if (review == null)
            return false;

        review.AuthorResponse = response;
        _context.WorkReviews.Update(review);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Добавить "полезный" голос к рецензии.
    /// </summary>
    public async Task<bool> MarkReviewAsHelpfulAsync(string reviewId)
    {
        var review = await _context.WorkReviews.FindAsync(reviewId);
        if (review == null)
            return false;

        review.HelpfulCount++;
        _context.WorkReviews.Update(review);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Получить статистику рецензий по работе.
    /// </summary>
    public async Task<(int TotalReviews, decimal? AverageRating, int TopReviewsCount)> GetWorkReviewStatsAsync(
        string contestId,
        int workNumber)
    {
        var reviews = await _context.WorkReviews
            .Where(r => r.ContestId == contestId && r.WorkNumber == workNumber && r.IsApproved && r.IsPublic)
            .ToListAsync();

        var totalReviews = reviews.Count;
        var averageRating = reviews.Any(r => r.Rating.HasValue)
            ? (decimal?)reviews.Where(r => r.Rating.HasValue).Average(r => r.Rating.Value)
            : null;
        var topReviewsCount = reviews.Count(r => r.HelpfulCount >= 3);

        return (totalReviews, averageRating, topReviewsCount);
    }
}
