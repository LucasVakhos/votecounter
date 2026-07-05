import Database from "better-sqlite3";
import fs from "node:fs";
import path from "node:path";
import type {
  Contest,
  ContestComment,
  ContestSorrowMessage,
  ContestWork,
  UserRole,
  VoteEntry,
  WorkReview
} from "@rhymers/shared";

export const dataDir = path.resolve(process.cwd(), "data");
export const dbFile = path.join(dataDir, "rhymers.db");

fs.mkdirSync(dataDir, { recursive: true });

export const db = new Database(dbFile);
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

export type ContestRow = {
  id: string;
  number: string;
  name: string;
  host_name: string;
  started_at: string;
};

export type ContestWorkRow = {
  id: string;
  contest_id: string;
  number: number;
  title: string;
  author_name: string | null;
};

export type VoteRow = {
  id: string;
  contest_id: string;
  voter_name: string;
  work_number: number;
  points: number;
};

export type CommentRow = {
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

export type ReviewRow = {
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

export type SorrowRow = {
  id: string;
  contest_id: string;
  author_name: string;
  content: string;
  type: ContestSorrowMessage["type"];
  created_at: string;
  empathy_count: number;
};

export function mapContest(row: ContestRow, works: ContestWork[]): Contest {
  return {
    id: row.id,
    number: row.number,
    name: row.name,
    hostName: row.host_name,
    startedAt: row.started_at,
    works
  };
}

export function mapWork(row: ContestWorkRow): ContestWork {
  return {
    number: row.number,
    title: row.title,
    authorName: row.author_name ?? undefined
  };
}

export function mapVote(row: VoteRow): VoteEntry {
  return {
    id: row.id,
    contestId: row.contest_id,
    voterName: row.voter_name,
    workNumber: row.work_number,
    points: row.points
  };
}

export function mapComment(row: CommentRow): ContestComment {
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

export function mapReview(row: ReviewRow): WorkReview {
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

export function mapSorrow(row: SorrowRow): ContestSorrowMessage {
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