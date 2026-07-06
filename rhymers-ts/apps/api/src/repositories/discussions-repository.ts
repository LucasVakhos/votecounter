import type { AddReviewRequest, DeletedItem, ModerationAction, ModerationActionKind, ModerationTargetType, ReviewStatsResponse, User, WorkReview } from "@rhymers/shared";
import { db, mapComment, mapModerationAction, mapReview, type CommentRow, type ModerationActionRow, type ReviewRow } from "../db.js";

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
    isDeleted: false,
    deletedAt: undefined,
    deletedBy: undefined,
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

export type SoftDeleteResult = "ok" | "not_found" | "forbidden_target";
// legacy alias
export type SoftDeleteCommentResult = SoftDeleteResult;

export function softDeleteMortalComment(commentId: string, moderatorName: string, reason?: string): SoftDeleteResult {
  const target = db
    .prepare("SELECT author_role FROM contest_comments WHERE id = ?")
    .get(commentId) as { author_role: User["role"] } | undefined;

  if (!target) {
    return "not_found";
  }

  if (target.author_role === "moderator" || target.author_role === "admin") {
    return "forbidden_target";
  }

  const now = new Date().toISOString();
  const result = db
    .prepare(
      "UPDATE contest_comments SET is_deleted = 1, is_hidden = 1, is_approved = 0, deleted_at = ?, deleted_by = ?, delete_reason = ? WHERE id = ? AND is_deleted = 0"
    )
    .run(now, moderatorName, reason ?? null, commentId);

  if (result.changes > 0) {
    logModerationAction(moderatorName, "delete", "comment", commentId, reason);
    return "ok";
  }
  return "not_found";
}

export function restoreComment(commentId: string, moderatorName: string): SoftDeleteResult {
  const target = db
    .prepare("SELECT is_deleted FROM contest_comments WHERE id = ?")
    .get(commentId) as { is_deleted: 0 | 1 } | undefined;

  if (!target) {
    return "not_found";
  }

  const result = db
    .prepare(
      "UPDATE contest_comments SET is_deleted = 0, is_hidden = 0, is_approved = 1, deleted_at = NULL, deleted_by = NULL, delete_reason = NULL WHERE id = ? AND is_deleted = 1"
    )
    .run(commentId);

  if (result.changes > 0) {
    logModerationAction(moderatorName, "restore", "comment", commentId);
    return "ok";
  }
  return "not_found";
}

export function getReviews(contestId: string, workNumber: number) {
  return (
    db
      .prepare(
        "SELECT id, contest_id, work_number, work_title, reviewer_name, reviewer_role, title, content, rating, strengths, areas_for_improvement, author_response, helpful_count, is_approved, is_hidden, is_deleted, deleted_at, deleted_by, created_at FROM work_reviews WHERE contest_id = ? AND work_number = ? ORDER BY created_at DESC"
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
    isDeleted: false,
    deletedAt: undefined,
    deletedBy: undefined,
    createdAt: new Date().toISOString()
  };

  db.prepare(
    "INSERT INTO work_reviews(id, contest_id, work_number, work_title, reviewer_name, reviewer_role, title, content, rating, strengths, areas_for_improvement, author_response, helpful_count, is_approved, is_hidden, is_deleted, deleted_at, deleted_by, created_at) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"
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
    0,
    null,
    null,
    review.createdAt
  );

  return review;
}

export function getReviewStats(contestId: string, workNumber: number): ReviewStatsResponse {
  const reviews = (
    db
      .prepare(
        "SELECT id, contest_id, work_number, work_title, reviewer_name, reviewer_role, title, content, rating, strengths, areas_for_improvement, author_response, helpful_count, is_approved, is_hidden, is_deleted, deleted_at, deleted_by, created_at FROM work_reviews WHERE contest_id = ? AND work_number = ?"
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

export type SoftDeleteReviewResult = SoftDeleteResult;

export function softDeleteMortalReview(reviewId: string, moderatorName: string, reason?: string): SoftDeleteResult {
  const target = db
    .prepare("SELECT reviewer_role FROM work_reviews WHERE id = ?")
    .get(reviewId) as { reviewer_role: User["role"] } | undefined;

  if (!target) {
    return "not_found";
  }

  if (target.reviewer_role === "moderator" || target.reviewer_role === "admin") {
    return "forbidden_target";
  }

  const now = new Date().toISOString();
  const result = db
    .prepare(
      "UPDATE work_reviews SET is_deleted = 1, is_hidden = 1, is_approved = 0, deleted_at = ?, deleted_by = ?, delete_reason = ? WHERE id = ? AND is_deleted = 0"
    )
    .run(now, moderatorName, reason ?? null, reviewId);

  if (result.changes > 0) {
    logModerationAction(moderatorName, "delete", "review", reviewId, reason);
    return "ok";
  }
  return "not_found";
}

export function restoreReview(reviewId: string, moderatorName: string): SoftDeleteResult {
  const target = db
    .prepare("SELECT is_deleted FROM work_reviews WHERE id = ?")
    .get(reviewId) as { is_deleted: 0 | 1 } | undefined;

  if (!target) {
    return "not_found";
  }

  const result = db
    .prepare(
      "UPDATE work_reviews SET is_deleted = 0, is_hidden = 0, is_approved = 1, deleted_at = NULL, deleted_by = NULL, delete_reason = NULL WHERE id = ? AND is_deleted = 1"
    )
    .run(reviewId);

  if (result.changes > 0) {
    logModerationAction(moderatorName, "restore", "review", reviewId);
    return "ok";
  }
  return "not_found";
}

export function logModerationAction(
  moderatorName: string,
  action: ModerationActionKind,
  targetType: ModerationTargetType,
  targetId: string,
  reason?: string
): void {
  db.prepare(
    "INSERT INTO moderation_actions(id, moderator_name, action, target_type, target_id, reason, performed_at) VALUES (?, ?, ?, ?, ?, ?, ?)"
  ).run(crypto.randomUUID(), moderatorName, action, targetType, targetId, reason ?? null, new Date().toISOString());
}

type ModerationLogFilters = {
  targetType?: ModerationTargetType;
  targetId?: string;
  moderatorName?: string;
  action?: ModerationActionKind;
  reason?: string;
  from?: string;
  to?: string;
};

type DeletedItemFilters = {
  contestId?: string;
  targetType?: ModerationTargetType;
  authorName?: string;
  reason?: string;
  from?: string;
  to?: string;
};

function includesText(value: string | undefined, needle: string | undefined): boolean {
  if (!needle) {
    return true;
  }

  return value?.toLowerCase().includes(needle.toLowerCase()) ?? false;
}

function isWithinDateRange(value: string, from?: string, to?: string): boolean {
  const timestamp = new Date(value).getTime();
  if (Number.isNaN(timestamp)) {
    return false;
  }

  const fromTimestamp = from ? new Date(from).getTime() : undefined;
  const toTimestamp = to ? new Date(to).getTime() : undefined;

  if (typeof fromTimestamp === "number" && !Number.isNaN(fromTimestamp) && timestamp < fromTimestamp) {
    return false;
  }

  if (typeof toTimestamp === "number" && !Number.isNaN(toTimestamp) && timestamp > toTimestamp) {
    return false;
  }

  return true;
}

export function getModerationLog(limit = 100, filters: ModerationLogFilters = {}): ModerationAction[] {
  return (
    db
      .prepare("SELECT id, moderator_name, action, target_type, target_id, reason, performed_at FROM moderation_actions ORDER BY performed_at DESC LIMIT ?")
      .all(limit) as ModerationActionRow[]
  )
    .map(mapModerationAction)
    .filter((entry) => {
      if (filters.targetType && entry.targetType !== filters.targetType) {
        return false;
      }

      if (filters.targetId && entry.targetId !== filters.targetId) {
        return false;
      }

      if (filters.action && entry.action !== filters.action) {
        return false;
      }

      if (!includesText(entry.moderatorName, filters.moderatorName)) {
        return false;
      }

      if (!includesText(entry.reason, filters.reason)) {
        return false;
      }

      return isWithinDateRange(entry.performedAt, filters.from, filters.to);
    });
}

export function getDeletedItems(filters: DeletedItemFilters = {}): DeletedItem[] {
  const comments = db
    .prepare(
      "SELECT 'comment' AS target_type, id, contest_id, author_name, deleted_by, deleted_at, delete_reason, content FROM contest_comments WHERE is_deleted = 1" +
      (filters.contestId ? " AND contest_id = ?" : "")
    )
    .all(...(filters.contestId ? [filters.contestId] : [])) as Array<{
      target_type: "comment";
      id: string;
      contest_id: string;
      author_name: string;
      deleted_by: string | null;
      deleted_at: string | null;
      delete_reason: string | null;
      content: string;
    }>;

  const reviews = db
    .prepare(
      "SELECT 'review' AS target_type, id, contest_id, reviewer_name AS author_name, deleted_by, deleted_at, delete_reason, title AS content FROM work_reviews WHERE is_deleted = 1" +
      (filters.contestId ? " AND contest_id = ?" : "")
    )
    .all(...(filters.contestId ? [filters.contestId] : [])) as Array<{
      target_type: "review";
      id: string;
      contest_id: string;
      author_name: string;
      deleted_by: string | null;
      deleted_at: string | null;
      delete_reason: string | null;
      content: string;
    }>;

  return [...comments, ...reviews]
    .map((row) => ({
      targetType: row.target_type,
      targetId: row.id,
      contestId: row.contest_id,
      authorName: row.author_name,
      deletedBy: row.deleted_by ?? "unknown",
      deletedAt: row.deleted_at ?? "",
      reason: row.delete_reason ?? undefined,
      originalContent: row.content
    }))
    .filter((item) => {
      if (filters.targetType && item.targetType !== filters.targetType) {
        return false;
      }

      if (!includesText(item.authorName, filters.authorName)) {
        return false;
      }

      if (!includesText(item.reason, filters.reason)) {
        return false;
      }

      return isWithinDateRange(item.deletedAt, filters.from, filters.to);
    });
}

export function setAuthorResponse(reviewId: string, responseText: string): boolean {
  const result = db.prepare("UPDATE work_reviews SET author_response = ? WHERE id = ?").run(responseText, reviewId);
  return result.changes > 0;
}
