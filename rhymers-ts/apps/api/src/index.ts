import cors from "cors";
import express from "express";
import Database from "better-sqlite3";
import fs from "node:fs";
import path from "node:path";
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
const dataDir = path.resolve(process.cwd(), "data");
const dbFile = path.join(dataDir, "rhymers.db");

app.use(cors());
app.use(express.json());

fs.mkdirSync(dataDir, { recursive: true });

const db = new Database(dbFile);
db.pragma("journal_mode = WAL");

db.exec(`
CREATE TABLE IF NOT EXISTS contests (
  id TEXT PRIMARY KEY,
  number TEXT NOT NULL,
  name TEXT NOT NULL,
  host_name TEXT NOT NULL,
  started_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS contest_works (
  id TEXT PRIMARY KEY,
  contest_id TEXT NOT NULL,
  number INTEGER NOT NULL,
  title TEXT NOT NULL,
  author_name TEXT,
  FOREIGN KEY(contest_id) REFERENCES contests(id)
);

CREATE TABLE IF NOT EXISTS votes (
  id TEXT PRIMARY KEY,
  contest_id TEXT NOT NULL,
  voter_name TEXT NOT NULL,
  work_number INTEGER NOT NULL,
  points INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS contest_comments (
  id TEXT PRIMARY KEY,
  contest_id TEXT NOT NULL,
  author_name TEXT NOT NULL,
  author_role TEXT NOT NULL,
  content TEXT NOT NULL,
  parent_comment_id TEXT,
  likes_count INTEGER NOT NULL,
  is_approved INTEGER NOT NULL,
  is_hidden INTEGER NOT NULL,
  created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS work_reviews (
  id TEXT PRIMARY KEY,
  contest_id TEXT NOT NULL,
  work_number INTEGER NOT NULL,
  work_title TEXT NOT NULL,
  reviewer_name TEXT NOT NULL,
  reviewer_role TEXT NOT NULL,
  title TEXT NOT NULL,
  content TEXT NOT NULL,
  rating INTEGER,
  strengths TEXT,
  areas_for_improvement TEXT,
  author_response TEXT,
  helpful_count INTEGER NOT NULL,
  is_approved INTEGER NOT NULL,
  is_hidden INTEGER NOT NULL,
  created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS sorrow_messages (
  id TEXT PRIMARY KEY,
  contest_id TEXT NOT NULL,
  author_name TEXT NOT NULL,
  content TEXT NOT NULL,
  type TEXT NOT NULL,
  created_at TEXT NOT NULL,
  empathy_count INTEGER NOT NULL
);
`);

type ContestRow = {
  id: string;
  number: string;
  name: string;
  host_name: string;
  started_at: string;
};

type ContestWorkRow = {
  id: string;
  contest_id: string;
  number: number;
  title: string;
  author_name: string | null;
};

type VoteRow = {
  id: string;
  contest_id: string;
  voter_name: string;
  work_number: number;
  points: number;
};

type CommentRow = {
  id: string;
  contest_id: string;
  author_name: string;
  author_role: UserRole;
  content: string;
  parent_comment_id: string | null;
  likes_count: number;
  is_approved: 0 | 1;
  is_hidden: 0 | 1;
  created_at: string;
};

type ReviewRow = {
  id: string;
  contest_id: string;
  work_number: number;
  work_title: string;
  reviewer_name: string;
  reviewer_role: UserRole;
  title: string;
  content: string;
  rating: number | null;
  strengths: string | null;
  areas_for_improvement: string | null;
  author_response: string | null;
  helpful_count: number;
  is_approved: 0 | 1;
  is_hidden: 0 | 1;
  created_at: string;
};

type SorrowRow = {
  id: string;
  contest_id: string;
  author_name: string;
  content: string;
  type: ContestSorrowMessage["type"];
  created_at: string;
  empathy_count: number;
};

function mapContest(row: ContestRow, works: ContestWork[]): Contest {
  return {
    id: row.id,
    number: row.number,
    name: row.name,
    hostName: row.host_name,
    startedAt: row.started_at,
    works
  };
}

function mapWork(row: ContestWorkRow): ContestWork {
  return {
    number: row.number,
    title: row.title,
    authorName: row.author_name ?? undefined
  };
}

function mapVote(row: VoteRow): VoteEntry {
  return {
    id: row.id,
    contestId: row.contest_id,
    voterName: row.voter_name,
    workNumber: row.work_number,
    points: row.points
  };
}

function mapComment(row: CommentRow): ContestComment {
  return {
    id: row.id,
    contestId: row.contest_id,
    authorName: row.author_name,
    authorRole: row.author_role,
    content: row.content,
    parentCommentId: row.parent_comment_id ?? undefined,
    likesCount: row.likes_count,
    isApproved: row.is_approved === 1,
    isHidden: row.is_hidden === 1,
    createdAt: row.created_at
  };
}

function mapReview(row: ReviewRow): WorkReview {
  return {
    id: row.id,
    contestId: row.contest_id,
    workNumber: row.work_number,
    workTitle: row.work_title,
    reviewerName: row.reviewer_name,
    reviewerRole: row.reviewer_role,
    title: row.title,
    content: row.content,
    rating: row.rating ?? undefined,
    strengths: row.strengths ?? undefined,
    areasForImprovement: row.areas_for_improvement ?? undefined,
    authorResponse: row.author_response ?? undefined,
    helpfulCount: row.helpful_count,
    isApproved: row.is_approved === 1,
    isHidden: row.is_hidden === 1,
    createdAt: row.created_at
  };
}

function mapSorrow(row: SorrowRow): ContestSorrowMessage {
  return {
    id: row.id,
    contestId: row.contest_id,
    authorName: row.author_name,
    content: row.content,
    type: row.type,
    createdAt: row.created_at,
    empathyCount: row.empathy_count
  };
}

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
  const contestRows = db.prepare("SELECT id, number, name, host_name, started_at FROM contests ORDER BY started_at DESC").all() as ContestRow[];
  const workRows = db.prepare("SELECT id, contest_id, number, title, author_name FROM contest_works").all() as ContestWorkRow[];
  const worksByContest = new Map<string, ContestWork[]>();

  for (const work of workRows) {
    const current = worksByContest.get(work.contest_id) ?? [];
    current.push(mapWork(work));
    worksByContest.set(work.contest_id, current);
  }

  res.json(contestRows.map((row) => mapContest(row, worksByContest.get(row.id) ?? [])));
});

app.get("/api/contests/:id", (req, res) => {
  const contestRow = db
    .prepare("SELECT id, number, name, host_name, started_at FROM contests WHERE id = ?")
    .get(req.params.id) as ContestRow | undefined;

  if (!contestRow) {
    res.status(404).json({ error: `Contest with ID ${req.params.id} not found` });
    return;
  }

  const works = (
    db
      .prepare("SELECT id, contest_id, number, title, author_name FROM contest_works WHERE contest_id = ? ORDER BY number ASC")
      .all(req.params.id) as ContestWorkRow[]
  ).map(mapWork);

  const contest = mapContest(contestRow, works);
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
    number: String((db.prepare("SELECT COUNT(1) AS count FROM contests").get() as { count: number }).count + 1).padStart(3, "0"),
    name: body.name.trim(),
    hostName: body.hostName?.trim() || "Unknown",
    startedAt: new Date().toISOString(),
    works: []
  };

  db.prepare("INSERT INTO contests(id, number, name, host_name, started_at) VALUES (?, ?, ?, ?, ?)").run(
    contest.id,
    contest.number,
    contest.name,
    contest.hostName,
    contest.startedAt
  );

  res.status(201).json(contest);
});

app.put("/api/contests/:id", (req, res) => {
  const contestRow = db
    .prepare("SELECT id, number, name, host_name, started_at FROM contests WHERE id = ?")
    .get(req.params.id) as ContestRow | undefined;

  if (!contestRow) {
    res.status(404).json({ error: `Contest with ID ${req.params.id} not found` });
    return;
  }

  const body = req.body as UpdateContestRequest;
  const newName = body.name?.trim() || contestRow.name;
  const newHost = body.hostName?.trim() || contestRow.host_name;

  db.prepare("UPDATE contests SET name = ?, host_name = ? WHERE id = ?").run(newName, newHost, req.params.id);

  const works = (
    db
      .prepare("SELECT id, contest_id, number, title, author_name FROM contest_works WHERE contest_id = ? ORDER BY number ASC")
      .all(req.params.id) as ContestWorkRow[]
  ).map(mapWork);

  res.json(
    mapContest(
      {
        ...contestRow,
        name: newName,
        host_name: newHost
      },
      works
    )
  );
});

app.post("/api/contests/:id/works", (req, res) => {
  const contestExists = db.prepare("SELECT 1 as value FROM contests WHERE id = ?").get(req.params.id) as { value: number } | undefined;
  if (!contestExists) {
    res.status(404).json({ error: `Contest with ID ${req.params.id} not found` });
    return;
  }

  const work = req.body as ContestWork;
  if (!work || typeof work.number !== "number" || !work.title?.trim()) {
    res.status(400).json({ error: "Work number and title are required" });
    return;
  }

  db.prepare("INSERT INTO contest_works(id, contest_id, number, title, author_name) VALUES (?, ?, ?, ?, ?)").run(
    crypto.randomUUID(),
    req.params.id,
    work.number,
    work.title.trim(),
    work.authorName?.trim() ?? null
  );

  const contestRow = db
    .prepare("SELECT id, number, name, host_name, started_at FROM contests WHERE id = ?")
    .get(req.params.id) as ContestRow;
  const works = (
    db
      .prepare("SELECT id, contest_id, number, title, author_name FROM contest_works WHERE contest_id = ? ORDER BY number ASC")
      .all(req.params.id) as ContestWorkRow[]
  ).map(mapWork);

  res.json(mapContest(contestRow, works));
});

app.get("/api/contests/:id/works", (req, res) => {
  const contestExists = db.prepare("SELECT 1 as value FROM contests WHERE id = ?").get(req.params.id) as { value: number } | undefined;
  if (!contestExists) {
    res.status(404).json({ error: `Contest with ID ${req.params.id} not found` });
    return;
  }

  const works = (
    db
      .prepare("SELECT id, contest_id, number, title, author_name FROM contest_works WHERE contest_id = ? ORDER BY number ASC")
      .all(req.params.id) as ContestWorkRow[]
  ).map(mapWork);

  res.json(works);
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

  const insertVote = db.prepare("INSERT INTO votes(id, contest_id, voter_name, work_number, points) VALUES (?, ?, ?, ?, ?)");
  for (const vote of imported) {
    insertVote.run(vote.id, vote.contestId, vote.voterName, vote.workNumber, vote.points);
  }

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
  const rows = db
    .prepare("SELECT id, contest_id, voter_name, work_number, points FROM votes WHERE contest_id = ?")
    .all(req.params.contestId) as VoteRow[];

  res.json(rows.map(mapVote));
});

app.post("/api/votes/results", (req, res) => {
  const contestId = String(req.body?.contestId ?? "").trim();
  if (!contestId) {
    res.status(400).json({ error: "Contest ID is required" });
    return;
  }

  const votes = (
    db.prepare("SELECT id, contest_id, voter_name, work_number, points FROM votes WHERE contest_id = ?").all(contestId) as VoteRow[]
  ).map(mapVote);
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
  const comments = (
    db
      .prepare(
        "SELECT id, contest_id, author_name, author_role, content, parent_comment_id, likes_count, is_approved, is_hidden, created_at FROM contest_comments WHERE contest_id = ? ORDER BY created_at DESC"
      )
      .all(req.params.contestId) as CommentRow[]
  ).map(mapComment);
  res.json(comments);
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

app.post("/api/discussions/comments/:commentId/like", (req, res) => {
  const result = db.prepare("UPDATE contest_comments SET likes_count = likes_count + 1 WHERE id = ?").run(req.params.commentId);
  if (result.changes > 0) {
    res.json({ ok: true });
    return;
  }
  res.status(404).json({ error: "Comment not found" });
});

app.post("/api/discussions/comments/:commentId/approve", (req, res) => {
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

app.post("/api/discussions/comments/:commentId/hide", (req, res) => {
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

app.get("/api/discussions/contests/:contestId/works/:workNumber/reviews", (req, res) => {
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

app.get("/api/discussions/contests/:contestId/works/:workNumber/review-stats", (req, res) => {
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

app.post("/api/discussions/reviews/:reviewId/helpful", (req, res) => {
  const result = db.prepare("UPDATE work_reviews SET helpful_count = helpful_count + 1 WHERE id = ?").run(req.params.reviewId);
  if (result.changes > 0) {
    res.json({ ok: true });
    return;
  }
  res.status(404).json({ error: "Review not found" });
});

app.post("/api/discussions/reviews/:reviewId/approve", (req, res) => {
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

app.post("/api/discussions/reviews/:reviewId/hide", (req, res) => {
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

  const result = db.prepare("UPDATE work_reviews SET author_response = ? WHERE id = ?").run(responseText, req.params.reviewId);
  if (result.changes > 0) {
    res.json({ ok: true });
    return;
  }
  res.status(404).json({ error: "Review not found" });
});

app.get("/health", (_req, res) => {
  res.json({ ok: true, service: "rhymers-ts-api", storage: "sqlite", dbFile });
});

app.get("/api/contests/:contestId/sorrow", (req, res) => {
  const rows = (
    db
      .prepare(
        "SELECT id, contest_id, author_name, content, type, created_at, empathy_count FROM sorrow_messages WHERE contest_id = ? ORDER BY created_at DESC"
      )
      .all(req.params.contestId) as SorrowRow[]
  ).map(mapSorrow);
  res.json(rows);
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

  db.prepare(
    "INSERT INTO sorrow_messages(id, contest_id, author_name, content, type, created_at, empathy_count) VALUES (?, ?, ?, ?, ?, ?, ?)"
  ).run(message.id, message.contestId, message.authorName, message.content, message.type, message.createdAt, message.empathyCount);

  res.status(201).json(message);
});

app.listen(port, () => {
  console.log(`rhymers-ts api listening on http://localhost:${port}`);
});
