import express from "express";
import type { AddCommentRequest, AddReviewRequest, ReviewStatsResponse, WorkReview } from "@rhymers/shared";
import { getCurrentUser, requireRole } from "../auth.js";
import { db, mapComment, mapReview, type CommentRow, type ReviewRow } from "../db.js";

export const discussionsRouter = express.Router();

discussionsRouter.get("/contests/:contestId/comments", (req, res) => {
  const comments = (
    db
      .prepare(
        "SELECT id, contest_id, author_name, author_role, content, parent_comment_id, likes_count, is_approved, is_hidden, created_at FROM contest_comments WHERE contest_id = ? ORDER BY created_at DESC"
      )
      .all(req.params.contestId) as CommentRow[]
  ).map(mapComment);
  res.json(comments);
});

discussionsRouter.post("/contests/:contestId/comments", (req, res) => {
  const user = getCurrentUser(req);
  if (!user) {
    res.status(401).json({ error: "Authentication required. Set X-User-Name header." });
    return;
  }

  const body = req.body as AddCommentRequest;
  if (!body?.content?.trim()) {
    res.status(400).json({ error: "Comment content is required" });
    return;
  }

  const comment = {
    id: crypto.randomUUID(),
    contestId: req.params.contestId,
    authorName: user.displayName,
    authorRole: user.role,
    content: body.content.trim(),
    parentCommentId: body.parentCommentId,
    likesCount: 0,
    isApproved: true,
    isHidden: false,
    createdAt: new Date().toISOString()
  };

  db.prepare(
    "INSERT INTO contest_comments(id, contest_id, author_name, author_role, content, parent_comment_id, likes_count, is_approved, is_hidden, created_at) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"
  ).run(
    comment.id,
    comment.contestId,
    comment.authorName,
    comment.authorRole,
    comment.content,
    comment.parentCommentId ?? null,
    comment.likesCount,
    comment.isApproved ? 1 : 0,
    comment.isHidden ? 1 : 0,
    comment.createdAt
  );

  res.status(201).json(comment);
});

discussionsRouter.post("/comments/:commentId/like", (req, res) => {
  const result = db.prepare("UPDATE contest_comments SET likes_count = likes_count + 1 WHERE id = ?").run(req.params.commentId);
  if (result.changes > 0) {
    res.json({ ok: true });
    return;
  }
  res.status(404).json({ error: "Comment not found" });
});

discussionsRouter.post("/comments/:commentId/approve", (req, res) => {
  const user = getCurrentUser(req);
  if (!requireRole(user, "moderator")) {
    res.status(403).json({ error: "Moderator role required" });
    return;
  }

  const result = db.prepare("UPDATE contest_comments SET is_approved = 1, is_hidden = 0 WHERE id = ?").run(req.params.commentId);
  if (result.changes > 0) {
    res.json({ ok: true });
    return;
  }
  res.status(404).json({ error: "Comment not found" });
});

discussionsRouter.post("/comments/:commentId/hide", (req, res) => {
  const user = getCurrentUser(req);
  if (!requireRole(user, "moderator")) {
    res.status(403).json({ error: "Moderator role required" });
    return;
  }

  const result = db.prepare("UPDATE contest_comments SET is_hidden = 1 WHERE id = ?").run(req.params.commentId);
  if (result.changes > 0) {
    res.json({ ok: true });
    return;
  }
  res.status(404).json({ error: "Comment not found" });
});

discussionsRouter.get("/contests/:contestId/works/:workNumber/reviews", (req, res) => {
  const contestId = req.params.contestId;
  const workNumber = Number.parseInt(req.params.workNumber, 10);
  const reviews = (
    db
      .prepare(
        "SELECT id, contest_id, work_number, work_title, reviewer_name, reviewer_role, title, content, rating, strengths, areas_for_improvement, author_response, helpful_count, is_approved, is_hidden, created_at FROM work_reviews WHERE contest_id = ? AND work_number = ? ORDER BY created_at DESC"
      )
      .all(contestId, workNumber) as ReviewRow[]
  ).map(mapReview);

  res.json(reviews);
});

discussionsRouter.post("/contests/:contestId/works/:workNumber/reviews", (req, res) => {
  const user = getCurrentUser(req);
  if (!user) {
    res.status(401).json({ error: "Authentication required. Set X-User-Name header." });
    return;
  }
  if (!requireRole(user, "author")) {
    res.status(403).json({ error: "Author role required" });
    return;
  }

  const body = req.body as AddReviewRequest;
  if (!body?.title?.trim() || !body?.content?.trim()) {
    res.status(400).json({ error: "Review title and content are required" });
    return;
  }

  const contestId = req.params.contestId;
  const workNumber = Number.parseInt(req.params.workNumber, 10);
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
    review.authorResponse ?? null,
    review.helpfulCount,
    review.isApproved ? 1 : 0,
    review.isHidden ? 1 : 0,
    review.createdAt
  );

  res.status(201).json(review);
});

discussionsRouter.get("/contests/:contestId/works/:workNumber/review-stats", (req, res) => {
  const contestId = req.params.contestId;
  const workNumber = Number.parseInt(req.params.workNumber, 10);
  const reviews = (
    db
      .prepare(
        "SELECT id, contest_id, work_number, work_title, reviewer_name, reviewer_role, title, content, rating, strengths, areas_for_improvement, author_response, helpful_count, is_approved, is_hidden, created_at FROM work_reviews WHERE contest_id = ? AND work_number = ?"
      )
      .all(contestId, workNumber) as ReviewRow[]
  ).map(mapReview);
  const ratings = reviews.map((x) => x.rating).filter((x): x is number => typeof x === "number");

  const stats: ReviewStatsResponse = {
    totalReviews: reviews.length,
    averageRating: ratings.length > 0 ? Number((ratings.reduce((a, b) => a + b, 0) / ratings.length).toFixed(2)) : undefined,
    topReviewsCount: reviews.filter((x) => (x.rating ?? 0) >= 8).length
  };

  res.json(stats);
});

discussionsRouter.post("/reviews/:reviewId/helpful", (req, res) => {
  const result = db.prepare("UPDATE work_reviews SET helpful_count = helpful_count + 1 WHERE id = ?").run(req.params.reviewId);
  if (result.changes > 0) {
    res.json({ ok: true });
    return;
  }
  res.status(404).json({ error: "Review not found" });
});

discussionsRouter.post("/reviews/:reviewId/approve", (req, res) => {
  const user = getCurrentUser(req);
  if (!requireRole(user, "moderator")) {
    res.status(403).json({ error: "Moderator role required" });
    return;
  }

  const result = db.prepare("UPDATE work_reviews SET is_approved = 1, is_hidden = 0 WHERE id = ?").run(req.params.reviewId);
  if (result.changes > 0) {
    res.json({ ok: true });
    return;
  }
  res.status(404).json({ error: "Review not found" });
});

discussionsRouter.post("/reviews/:reviewId/hide", (req, res) => {
  const user = getCurrentUser(req);
  if (!requireRole(user, "moderator")) {
    res.status(403).json({ error: "Moderator role required" });
    return;
  }

  const result = db.prepare("UPDATE work_reviews SET is_hidden = 1 WHERE id = ?").run(req.params.reviewId);
  if (result.changes > 0) {
    res.json({ ok: true });
    return;
  }
  res.status(404).json({ error: "Review not found" });
});

discussionsRouter.post("/reviews/:reviewId/author-response", (req, res) => {
  const user = getCurrentUser(req);
  if (!user) {
    res.status(401).json({ error: "Authentication required. Set X-User-Name header." });
    return;
  }

  const responseText = String(req.body?.response ?? "").trim();
  if (!responseText) {
    res.status(400).json({ error: "Response text is required" });
    return;
  }

  const result = db.prepare("UPDATE work_reviews SET author_response = ? WHERE id = ?").run(responseText, req.params.reviewId);
  if (result.changes > 0) {
    res.json({ ok: true });
    return;
  }
  res.status(404).json({ error: "Review not found" });
});
