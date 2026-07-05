import type { AddReviewRequest, ReviewStatsResponse, User, WorkReview } from "@rhymers/shared";
import { db, mapComment, mapReview, type CommentRow, type ReviewRow } from "../db.js";

export function getComments(contestId: string) {
  return (
    db
      .prepare(
        "SELECT id, contest_id, author_name, author_role, content, parent_comment_id, likes_count, is_approved, is_hidden, is_deleted, deleted_at, deleted_by, created_at FROM contest_comments WHERE contest_id = ? ORDER BY created_at DESC"
      )
      .all(contestId) as CommentRow[]
  ).map(mapComment);
}

export function addComment(contestId: string, user: User, content: string, parentCommentId?: string) {
  const comment = {
    id: crypto.randomUUID(),
    contestId,
    authorName: user.displayName,
    authorRole: user.role,
    content,
    parentCommentId,
    likesCount: 0,
    isApproved: true,
    isHidden: false,
    createdAt: new Date().toISOString()
  };

  db.prepare(
    "INSERT INTO contest_comments(id, contest_id, author_name, author_role, content, parent_comment_id, likes_count, is_approved, is_hidden, is_deleted, deleted_at, deleted_by, created_at) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"
  ).run(
    comment.id,
    comment.contestId,
    comment.authorName,
    comment.authorRole,
    comment.content,
    comment.parentCommentId ?? null,
    comment.likesCount,
    1,
    0,
    0,
    null,
    null,
    comment.createdAt
  );

  return comment;
}

export function likeComment(commentId: string): boolean {
  const result = db.prepare("UPDATE contest_comments SET likes_count = likes_count + 1 WHERE id = ?").run(commentId);
  return result.changes > 0;
}

export function approveComment(commentId: string): boolean {
  const result = db.prepare("UPDATE contest_comments SET is_approved = 1, is_hidden = 0 WHERE id = ?").run(commentId);
  return result.changes > 0;
}

export function hideComment(commentId: string): boolean {
  const result = db.prepare("UPDATE contest_comments SET is_hidden = 1 WHERE id = ?").run(commentId);
  return result.changes > 0;
}

export type SoftDeleteCommentResult = "ok" | "not_found" | "forbidden_target";

export function softDeleteMortalComment(commentId: string, moderatorName: string): SoftDeleteCommentResult {
  const target = db
    .prepare("SELECT author_role FROM contest_comments WHERE id = ?")
    .get(commentId) as { author_role: User["role"] } | undefined;

  if (!target) {
    return "not_found";
  }

  if (target.author_role === "moderator" || target.author_role === "admin") {
    return "forbidden_target";
  }

  const result = db
    .prepare(
      "UPDATE contest_comments SET is_deleted = 1, is_hidden = 1, is_approved = 0, deleted_at = ?, deleted_by = ? WHERE id = ? AND is_deleted = 0"
    )
    .run(new Date().toISOString(), moderatorName, commentId);

  return result.changes > 0 ? "ok" : "not_found";
}

export function getReviews(contestId: string, workNumber: number) {
  return (
    db
      .prepare(
        "SELECT id, contest_id, work_number, work_title, reviewer_name, reviewer_role, title, content, rating, strengths, areas_for_improvement, author_response, helpful_count, is_approved, is_hidden, created_at FROM work_reviews WHERE contest_id = ? AND work_number = ? ORDER BY created_at DESC"
      )
      .all(contestId, workNumber) as ReviewRow[]
  ).map(mapReview);
}

export function addReview(contestId: string, workNumber: number, user: User, body: AddReviewRequest): WorkReview {
  const review: WorkReview = {
    id: crypto.randomUUID(),
    contestId,
    workNumber,
    workTitle: body.workTitle?.trim() || `Work #${workNumber}`,
    reviewerName: user.displayName,
    reviewerRole: user.role,
    title: body.title.trim(),
    content: body.content.trim(),
    rating: body.rating,
    strengths: body.strengths,
    areasForImprovement: body.areasForImprovement,
    authorResponse: undefined,
    helpfulCount: 0,
    isApproved: true,
    isHidden: false,
    createdAt: new Date().toISOString()
  };

  db.prepare(
    "INSERT INTO work_reviews(id, contest_id, work_number, work_title, reviewer_name, reviewer_role, title, content, rating, strengths, areas_for_improvement, author_response, helpful_count, is_approved, is_hidden, created_at) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"
  ).run(
    review.id,
    review.contestId,
    review.workNumber,
    review.workTitle,
    review.reviewerName,
    review.reviewerRole,
    review.title,
    review.content,
    review.rating ?? null,
    review.strengths ?? null,
    review.areasForImprovement ?? null,
    null,
    0,
    1,
    0,
    review.createdAt
  );

  return review;
}

export function getReviewStats(contestId: string, workNumber: number): ReviewStatsResponse {
  const reviews = (
    db
      .prepare(
        "SELECT id, contest_id, work_number, work_title, reviewer_name, reviewer_role, title, content, rating, strengths, areas_for_improvement, author_response, helpful_count, is_approved, is_hidden, created_at FROM work_reviews WHERE contest_id = ? AND work_number = ?"
      )
      .all(contestId, workNumber) as ReviewRow[]
  ).map(mapReview);

  const ratings = reviews.map((x) => x.rating).filter((x): x is number => typeof x === "number");
  return {
    totalReviews: reviews.length,
    averageRating: ratings.length > 0 ? Number((ratings.reduce((a, b) => a + b, 0) / ratings.length).toFixed(2)) : undefined,
    topReviewsCount: reviews.filter((x) => (x.rating ?? 0) >= 8).length
  };
}

export function markReviewHelpful(reviewId: string): boolean {
  const result = db.prepare("UPDATE work_reviews SET helpful_count = helpful_count + 1 WHERE id = ?").run(reviewId);
  return result.changes > 0;
}

export function approveReview(reviewId: string): boolean {
  const result = db.prepare("UPDATE work_reviews SET is_approved = 1, is_hidden = 0 WHERE id = ?").run(reviewId);
  return result.changes > 0;
}

export function hideReview(reviewId: string): boolean {
  const result = db.prepare("UPDATE work_reviews SET is_hidden = 1 WHERE id = ?").run(reviewId);
  return result.changes > 0;
}

export function setAuthorResponse(reviewId: string, responseText: string): boolean {
  const result = db.prepare("UPDATE work_reviews SET author_response = ? WHERE id = ?").run(responseText, reviewId);
  return result.changes > 0;
}
