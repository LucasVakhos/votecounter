import Database from "better-sqlite3";
import { dbFile, ensureDataDir } from "./db-config.js";
import { getCurrentSchemaVersion, runMigrations } from "./migrations.js";
import type {
  Contest,
  ContestComment,
  ContestSorrowMessage,
  ContestWork,
  ModerationAction,
  ModerationActionKind,
  ModerationTargetType,
  UserRole,
  VoteEntry,
  WorkReview
} from "@rhymers/shared";

ensureDataDir();
export { dbFile };

export const db = new Database(dbFile);
db.pragma("journal_mode = WAL");
runMigrations(db);
export const schemaVersion = getCurrentSchemaVersion(db);

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
  is_deleted: 0 | 1;
  deleted_at: string | null;
  deleted_by: string | null;
  delete_reason: string | null;
  created_at: string;
};

export type ModerationActionRow = {
  id: string;
  moderator_name: string;
  action: ModerationActionKind;
  target_type: ModerationTargetType;
  target_id: string;
  reason: string | null;
  performed_at: string;
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
  is_deleted: 0 | 1;
  deleted_at: string | null;
  deleted_by: string | null;
  delete_reason: string | null;
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
  const isDeleted = row.is_deleted === 1;
  return {
    id: row.id,
    contestId: row.contest_id,
    authorName: row.author_name,
    authorRole: row.author_role,
    content: isDeleted ? "[Deleted by moderation]" : row.content,
    parentCommentId: row.parent_comment_id ?? undefined,
    likesCount: row.likes_count,
    isApproved: row.is_approved === 1,
    isHidden: row.is_hidden === 1,
    isDeleted,
    deletedAt: row.deleted_at ?? undefined,
    deletedBy: row.deleted_by ?? undefined,
    createdAt: row.created_at
  };
}

export function mapModerationAction(row: ModerationActionRow): ModerationAction {
  return {
    id: row.id,
    moderatorName: row.moderator_name,
    action: row.action,
    targetType: row.target_type,
    targetId: row.target_id,
    reason: row.reason ?? undefined,
    performedAt: row.performed_at
  };
}

export function mapReview(row: ReviewRow): WorkReview {
  const isDeleted = row.is_deleted === 1;
  return {
    id: row.id,
    contestId: row.contest_id,
    workNumber: row.work_number,
    workTitle: row.work_title,
    reviewerName: row.reviewer_name,
    reviewerRole: row.reviewer_role,
    title: isDeleted ? "[Deleted by moderation]" : row.title,
    content: isDeleted ? "[Deleted by moderation]" : row.content,
    rating: row.rating ?? undefined,
    strengths: isDeleted ? undefined : (row.strengths ?? undefined),
    areasForImprovement: isDeleted ? undefined : (row.areas_for_improvement ?? undefined),
    authorResponse: isDeleted ? undefined : (row.author_response ?? undefined),
    helpfulCount: row.helpful_count,
    isApproved: row.is_approved === 1,
    isHidden: row.is_hidden === 1,
    isDeleted,
    deletedAt: row.deleted_at ?? undefined,
    deletedBy: row.deleted_by ?? undefined,
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