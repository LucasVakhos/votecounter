export type UserRole = "reader" | "author" | "moderator" | "admin";

export interface User {
  id: string;
  displayName: string;
  role: UserRole;
}

export type SorrowType =
  | "reflection"
  | "fear"
  | "disappointment"
  | "inspiration"
  | "support"
  | "selfAnalysis"
  | "impressions"
  | "lifeCircumstances";

export interface ContestSorrowMessage {
  id: string;
  contestId: string;
  authorName: string;
  content: string;
  type: SorrowType;
  createdAt: string;
  empathyCount: number;
}

export interface AddSorrowMessageRequest {
  contestId: string;
  content: string;
  type: SorrowType;
}

export interface ContestWork {
  number: number;
  title: string;
  authorName?: string;
}

export interface Contest {
  id: string;
  number: string;
  name: string;
  hostName: string;
  startedAt: string;
  works: ContestWork[];
}

export interface CreateContestRequest {
  name: string;
  hostName?: string;
}

export interface UpdateContestRequest {
  name?: string;
  hostName?: string;
}

export interface VoteEntry {
  id: string;
  contestId: string;
  voterName: string;
  workNumber: number;
  points: number;
}

export interface ParsedVoteBlock {
  voterName: string;
  rawLine: string;
  entries: Array<{ workNumber: number; points: number }>;
}

export interface ImportResult {
  contestId: string;
  blocks: ParsedVoteBlock[];
  errors: string[];
}

export interface ContestRatingRow {
  workNumber: number;
  totalPoints: number;
}

export interface ContestResultsReport {
  contestId: string;
  generatedAt: string;
  rows: ContestRatingRow[];
}

export interface ContestComment {
  id: string;
  contestId: string;
  authorName: string;
  authorRole: UserRole;
  content: string;
  parentCommentId?: string;
  likesCount: number;
  isApproved: boolean;
  isHidden: boolean;
  isDeleted: boolean;
  deletedAt?: string;
  deletedBy?: string;
  createdAt: string;
}

export interface AddCommentRequest {
  content: string;
  parentCommentId?: string;
}

export interface WorkReview {
  id: string;
  contestId: string;
  workNumber: number;
  workTitle: string;
  reviewerName: string;
  reviewerRole: UserRole;
  title: string;
  content: string;
  rating?: number;
  strengths?: string;
  areasForImprovement?: string;
  authorResponse?: string;
  helpfulCount: number;
  isApproved: boolean;
  isHidden: boolean;
  isDeleted: boolean;
  deletedAt?: string;
  deletedBy?: string;
  createdAt: string;
}

export interface AddReviewRequest {
  title: string;
  content: string;
  rating?: number;
  strengths?: string;
  areasForImprovement?: string;
  workTitle?: string;
}

export interface ReviewStatsResponse {
  totalReviews: number;
  averageRating?: number;
  topReviewsCount: number;
}

export type ModerationActionKind = "delete" | "restore" | "hide" | "approve";
export type ModerationTargetType = "comment" | "review";

export interface ModerationAction {
  id: string;
  moderatorName: string;
  action: ModerationActionKind;
  targetType: ModerationTargetType;
  targetId: string;
  reason?: string;
  performedAt: string;
}

export interface ModeratorDeleteRequest {
  reason?: string;
}

export interface DeletedItem {
  targetType: ModerationTargetType;
  targetId: string;
  contestId: string;
  authorName: string;
  deletedBy: string;
  deletedAt: string;
  reason?: string;
  originalContent: string;
}
