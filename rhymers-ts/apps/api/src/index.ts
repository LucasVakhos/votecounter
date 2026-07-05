import cors from "cors";
import express from "express";
import type {
  AddCommentRequest,
  AddReviewRequest,
  AddSorrowMessageRequest,
  Contest,
  ContestComment,
  ContestResultsReport,
  ContestSorrowMessage,
  ContestWork,
  CreateContestRequest,
  ImportResult,
  ReviewStatsResponse,
  UpdateContestRequest,
  User,
  UserRole,
  VoteEntry,
  WorkReview
} from "@rhymers/shared";

const app = express();
const port = Number(process.env.PORT ?? 4000);

app.use(cors());
app.use(express.json());

const sorrowMessages: ContestSorrowMessage[] = [];
const contests: Contest[] = [];
const votesByContest = new Map<string, VoteEntry[]>();
const commentsByContest = new Map<string, ContestComment[]>();
const reviewsByContestWork = new Map<string, WorkReview[]>();

function getCurrentUser(req: express.Request): User | null {
  const userName = req.header("X-User-Name")?.trim();
  if (!userName) {
    return null;
  }

  const roleHeader = (req.header("X-User-Role") ?? "author").trim().toLowerCase();
  const role: UserRole =
    roleHeader === "admin" || roleHeader === "moderator" || roleHeader === "reader" || roleHeader === "author"
      ? roleHeader
      : "author";

  return {
    id: `user-${userName.toLowerCase().replace(/\s+/g, "-")}`,
    displayName: userName,
    role
  };
}

function requireRole(user: User | null, minRole: UserRole): boolean {
  if (!user) {
    return false;
  }

  const rank: Record<UserRole, number> = {
    reader: 1,
    author: 2,
    moderator: 3,
    admin: 4
  };

  return rank[user.role] >= rank[minRole];
}

function workKey(contestId: string, workNumber: number): string {
  return `${contestId}:${workNumber}`;
}

function parseVoteText(contestId: string, voteText: string): ImportResult {
  const lines = voteText
    .split(/\r?\n/)
    .map((x) => x.trim())
    .filter(Boolean);

  const result: ImportResult = {
    contestId,
    blocks: [],
    errors: []
  };

  for (const line of lines) {
    const [namePart, votesPart] = line.split(":");
    if (!namePart || !votesPart) {
      result.errors.push(`Invalid vote line: ${line}`);
      continue;
    }

    const voterName = namePart.trim();
    const workNumbers = votesPart
      .split(",")
      .map((x) => Number.parseInt(x.trim(), 10))
      .filter((x) => Number.isFinite(x) && x > 0);

    const entries = workNumbers.map((workNumber, idx) => ({
      workNumber,
      points: Math.max(1, 10 - idx)
    }));

    result.blocks.push({
      voterName,
      rawLine: line,
      entries
    });
  }

  return result;
}

// ===== Contests =====

app.get("/api/contests", (_req, res) => {
  res.json(contests);
});

app.get("/api/contests/:id", (req, res) => {
  const contest = contests.find((x) => x.id === req.params.id);
  if (!contest) {
    res.status(404).json({ error: `Contest with ID ${req.params.id} not found` });
    return;
  }
  res.json(contest);
});

app.post("/api/contests", (req, res) => {
  const body = req.body as CreateContestRequest;
  if (!body?.name?.trim()) {
    res.status(400).json({ error: "Contest name is required" });
    return;
  }

  const contest: Contest = {
    id: crypto.randomUUID().replaceAll("-", ""),
    number: String(contests.length + 1).padStart(3, "0"),
    name: body.name.trim(),
    hostName: body.hostName?.trim() || "Unknown",
    startedAt: new Date().toISOString(),
    works: []
  };

  contests.push(contest);
  res.status(201).json(contest);
});

app.put("/api/contests/:id", (req, res) => {
  const contest = contests.find((x) => x.id === req.params.id);
  if (!contest) {
    res.status(404).json({ error: `Contest with ID ${req.params.id} not found` });
    return;
  }

  const body = req.body as UpdateContestRequest;
  if (body.name?.trim()) {
    contest.name = body.name.trim();
  }
  if (body.hostName?.trim()) {
    contest.hostName = body.hostName.trim();
  }

  res.json(contest);
});

app.post("/api/contests/:id/works", (req, res) => {
  const contest = contests.find((x) => x.id === req.params.id);
  if (!contest) {
    res.status(404).json({ error: `Contest with ID ${req.params.id} not found` });
    return;
  }

  const work = req.body as ContestWork;
  if (!work || typeof work.number !== "number" || !work.title?.trim()) {
    res.status(400).json({ error: "Work number and title are required" });
    return;
  }

  contest.works.push({
    number: work.number,
    title: work.title.trim(),
    authorName: work.authorName?.trim()
  });

  res.json(contest);
});

app.get("/api/contests/:id/works", (req, res) => {
  const contest = contests.find((x) => x.id === req.params.id);
  if (!contest) {
    res.status(404).json({ error: `Contest with ID ${req.params.id} not found` });
    return;
  }

  res.json(contest.works);
});

// ===== Votes =====

app.post("/api/votes/import", (req, res) => {
  const contestId = String(req.body?.contestId ?? "").trim();
  const voteText = String(req.body?.voteText ?? "").trim();

  if (!contestId) {
    res.status(400).json({ error: "Contest ID is required" });
    return;
  }
  if (!voteText) {
    res.status(400).json({ error: "Vote text is required" });
    return;
  }

  const parsed = parseVoteText(contestId, voteText);
  const imported: VoteEntry[] = [];

  for (const block of parsed.blocks) {
    for (const entry of block.entries) {
      imported.push({
        id: crypto.randomUUID(),
        contestId,
        voterName: block.voterName,
        workNumber: entry.workNumber,
        points: entry.points
      });
    }
  }

  const existing = votesByContest.get(contestId) ?? [];
  votesByContest.set(contestId, existing.concat(imported));
  res.json(parsed);
});

app.post("/api/votes/validate", (req, res) => {
  const contestId = String(req.body?.contestId ?? "").trim();
  const importResult = req.body?.importResult as ImportResult | undefined;

  if (!contestId) {
    res.status(400).json({ error: "Contest ID is required" });
    return;
  }
  if (!importResult) {
    res.status(400).json({ error: "Import result is required" });
    return;
  }

  const validationErrors = [...(importResult.errors ?? [])];
  if ((importResult.blocks?.length ?? 0) === 0) {
    validationErrors.push("No vote blocks found");
  }

  res.json({
    ...importResult,
    contestId,
    errors: validationErrors
  } satisfies ImportResult);
});

app.get("/api/votes/contest/:contestId", (req, res) => {
  res.json(votesByContest.get(req.params.contestId) ?? []);
});

app.post("/api/votes/results", (req, res) => {
  const contestId = String(req.body?.contestId ?? "").trim();
  if (!contestId) {
    res.status(400).json({ error: "Contest ID is required" });
    return;
  }

  const votes = votesByContest.get(contestId) ?? [];
  const byWork = new Map<number, number>();
  for (const vote of votes) {
    byWork.set(vote.workNumber, (byWork.get(vote.workNumber) ?? 0) + vote.points);
  }

  const report: ContestResultsReport = {
    contestId,
    generatedAt: new Date().toISOString(),
    rows: [...byWork.entries()]
      .map(([workNumber, totalPoints]) => ({ workNumber, totalPoints }))
      .sort((a, b) => b.totalPoints - a.totalPoints)
  };

  res.json(report);
});

// ===== Discussions =====

app.get("/api/discussions/contests/:contestId/comments", (req, res) => {
  res.json(commentsByContest.get(req.params.contestId) ?? []);
});

app.post("/api/discussions/contests/:contestId/comments", (req, res) => {
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

  const contestId = req.params.contestId;
  const comments = commentsByContest.get(contestId) ?? [];
  const comment: ContestComment = {
    id: crypto.randomUUID(),
    contestId,
    authorName: user.displayName,
    authorRole: user.role,
    content: body.content.trim(),
    parentCommentId: body.parentCommentId,
    likesCount: 0,
    isApproved: true,
    isHidden: false,
    createdAt: new Date().toISOString()
  };

  comments.unshift(comment);
  commentsByContest.set(contestId, comments);
  res.status(201).json(comment);
});

app.post("/api/discussions/comments/:commentId/like", (req, res) => {
  const commentId = req.params.commentId;
  for (const comments of commentsByContest.values()) {
    const comment = comments.find((x) => x.id === commentId);
    if (comment) {
      comment.likesCount += 1;
      res.json({ ok: true });
      return;
    }
  }
  res.status(404).json({ error: "Comment not found" });
});

app.post("/api/discussions/comments/:commentId/approve", (req, res) => {
  const user = getCurrentUser(req);
  if (!requireRole(user, "moderator")) {
    res.status(403).json({ error: "Moderator role required" });
    return;
  }

  const commentId = req.params.commentId;
  for (const comments of commentsByContest.values()) {
    const comment = comments.find((x) => x.id === commentId);
    if (comment) {
      comment.isApproved = true;
      comment.isHidden = false;
      res.json({ ok: true });
      return;
    }
  }
  res.status(404).json({ error: "Comment not found" });
});

app.post("/api/discussions/comments/:commentId/hide", (req, res) => {
  const user = getCurrentUser(req);
  if (!requireRole(user, "moderator")) {
    res.status(403).json({ error: "Moderator role required" });
    return;
  }

  const commentId = req.params.commentId;
  for (const comments of commentsByContest.values()) {
    const comment = comments.find((x) => x.id === commentId);
    if (comment) {
      comment.isHidden = true;
      res.json({ ok: true });
      return;
    }
  }
  res.status(404).json({ error: "Comment not found" });
});

app.get("/api/discussions/contests/:contestId/works/:workNumber/reviews", (req, res) => {
  const contestId = req.params.contestId;
  const workNumber = Number.parseInt(req.params.workNumber, 10);
  res.json(reviewsByContestWork.get(workKey(contestId, workNumber)) ?? []);
});

app.post("/api/discussions/contests/:contestId/works/:workNumber/reviews", (req, res) => {
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
  const key = workKey(contestId, workNumber);
  const reviews = reviewsByContestWork.get(key) ?? [];

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

  reviews.unshift(review);
  reviewsByContestWork.set(key, reviews);
  res.status(201).json(review);
});

app.get("/api/discussions/contests/:contestId/works/:workNumber/review-stats", (req, res) => {
  const contestId = req.params.contestId;
  const workNumber = Number.parseInt(req.params.workNumber, 10);
  const reviews = reviewsByContestWork.get(workKey(contestId, workNumber)) ?? [];
  const ratings = reviews.map((x) => x.rating).filter((x): x is number => typeof x === "number");

  const stats: ReviewStatsResponse = {
    totalReviews: reviews.length,
    averageRating: ratings.length > 0 ? Number((ratings.reduce((a, b) => a + b, 0) / ratings.length).toFixed(2)) : undefined,
    topReviewsCount: reviews.filter((x) => (x.rating ?? 0) >= 8).length
  };

  res.json(stats);
});

app.post("/api/discussions/reviews/:reviewId/helpful", (req, res) => {
  const reviewId = req.params.reviewId;
  for (const reviews of reviewsByContestWork.values()) {
    const review = reviews.find((x) => x.id === reviewId);
    if (review) {
      review.helpfulCount += 1;
      res.json({ ok: true });
      return;
    }
  }
  res.status(404).json({ error: "Review not found" });
});

app.post("/api/discussions/reviews/:reviewId/approve", (req, res) => {
  const user = getCurrentUser(req);
  if (!requireRole(user, "moderator")) {
    res.status(403).json({ error: "Moderator role required" });
    return;
  }

  const reviewId = req.params.reviewId;
  for (const reviews of reviewsByContestWork.values()) {
    const review = reviews.find((x) => x.id === reviewId);
    if (review) {
      review.isApproved = true;
      review.isHidden = false;
      res.json({ ok: true });
      return;
    }
  }
  res.status(404).json({ error: "Review not found" });
});

app.post("/api/discussions/reviews/:reviewId/hide", (req, res) => {
  const user = getCurrentUser(req);
  if (!requireRole(user, "moderator")) {
    res.status(403).json({ error: "Moderator role required" });
    return;
  }

  const reviewId = req.params.reviewId;
  for (const reviews of reviewsByContestWork.values()) {
    const review = reviews.find((x) => x.id === reviewId);
    if (review) {
      review.isHidden = true;
      res.json({ ok: true });
      return;
    }
  }
  res.status(404).json({ error: "Review not found" });
});

app.post("/api/discussions/reviews/:reviewId/author-response", (req, res) => {
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

  const reviewId = req.params.reviewId;
  for (const reviews of reviewsByContestWork.values()) {
    const review = reviews.find((x) => x.id === reviewId);
    if (review) {
      review.authorResponse = responseText;
      res.json({ ok: true });
      return;
    }
  }
  res.status(404).json({ error: "Review not found" });
});

app.get("/health", (_req, res) => {
  res.json({ ok: true, service: "rhymers-ts-api" });
});

app.get("/api/contests/:contestId/sorrow", (req, res) => {
  const { contestId } = req.params;
  res.json(sorrowMessages.filter((x) => x.contestId === contestId));
});

app.post("/api/contests/:contestId/sorrow", (req, res) => {
  const { contestId } = req.params;
  const body = req.body as AddSorrowMessageRequest;

  if (!body?.content?.trim()) {
    res.status(400).json({ error: "content is required" });
    return;
  }

  const message: ContestSorrowMessage = {
    id: crypto.randomUUID(),
    contestId,
    authorName: "anonymous",
    content: body.content.trim(),
    type: body.type ?? "reflection",
    createdAt: new Date().toISOString(),
    empathyCount: 0
  };

  sorrowMessages.unshift(message);
  res.status(201).json(message);
});

app.listen(port, () => {
  console.log(`rhymers-ts api listening on http://localhost:${port}`);
});
