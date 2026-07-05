import express from "express";
import type { AddCommentRequest, AddReviewRequest, ReviewStatsResponse, WorkReview } from "@rhymers/shared";
import { getCurrentUser, requireRole } from "../auth.js";
import {
  addComment,
  addReview,
  approveComment,
  approveReview,
  getComments,
  getReviews,
  getReviewStats,
  hideComment,
  hideReview,
  likeComment,
  markReviewHelpful,
  softDeleteMortalComment,
  setAuthorResponse
} from "../repositories/discussions-repository.js";

export const discussionsRouter = express.Router();

discussionsRouter.get("/contests/:contestId/comments", (req, res) => {
  res.json(getComments(req.params.contestId));
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

  const comment = addComment(req.params.contestId, user, body.content.trim(), body.parentCommentId);
  res.status(201).json(comment);
});

discussionsRouter.post("/comments/:commentId/like", (req, res) => {
  if (likeComment(req.params.commentId)) {
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

  if (approveComment(req.params.commentId)) {
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

  if (hideComment(req.params.commentId)) {
    res.json({ ok: true });
    return;
  }
  res.status(404).json({ error: "Comment not found" });
});

discussionsRouter.post("/comments/:commentId/delete", (req, res) => {
  const user = getCurrentUser(req);
  if (!requireRole(user, "moderator")) {
    res.status(403).json({ error: "Moderator role required" });
    return;
  }

  if (!user) {
    res.status(403).json({ error: "Moderator role required" });
    return;
  }

  const result = softDeleteMortalComment(req.params.commentId, user.displayName);
  if (result === "ok") {
    res.json({ ok: true });
    return;
  }
  if (result === "forbidden_target") {
    res.status(403).json({ error: "Cannot delete moderator/admin comments" });
    return;
  }

  res.status(404).json({ error: "Comment not found" });
});

discussionsRouter.get("/contests/:contestId/works/:workNumber/reviews", (req, res) => {
  const contestId = req.params.contestId;
  const workNumber = Number.parseInt(req.params.workNumber, 10);
  res.json(getReviews(contestId, workNumber));
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
  const review: WorkReview = addReview(contestId, workNumber, user, body);
  res.status(201).json(review);
});

discussionsRouter.get("/contests/:contestId/works/:workNumber/review-stats", (req, res) => {
  const contestId = req.params.contestId;
  const workNumber = Number.parseInt(req.params.workNumber, 10);
  const stats: ReviewStatsResponse = getReviewStats(contestId, workNumber);

  res.json(stats);
});

discussionsRouter.post("/reviews/:reviewId/helpful", (req, res) => {
  if (markReviewHelpful(req.params.reviewId)) {
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

  if (approveReview(req.params.reviewId)) {
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

  if (hideReview(req.params.reviewId)) {
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

  if (setAuthorResponse(req.params.reviewId, responseText)) {
    res.json({ ok: true });
    return;
  }
  res.status(404).json({ error: "Review not found" });
});
