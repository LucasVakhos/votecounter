import type express from "express";
import type { User, UserRole } from "@rhymers/shared";

export function getCurrentUser(req: express.Request): User | null {
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

export function requireRole(user: User | null, minRole: UserRole): boolean {
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
