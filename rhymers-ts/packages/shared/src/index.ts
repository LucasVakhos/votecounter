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
