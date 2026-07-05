import type { ContestSorrowMessage } from "@rhymers/shared";
import { db, mapSorrow, type SorrowRow } from "../db.js";

export function getSorrowMessages(contestId: string): ContestSorrowMessage[] {
  const rows = (
    db
      .prepare(
        "SELECT id, contest_id, author_name, content, type, created_at, empathy_count FROM sorrow_messages WHERE contest_id = ? ORDER BY created_at DESC"
      )
      .all(contestId) as SorrowRow[]
  ).map(mapSorrow);

  return rows;
}

export function addSorrowMessage(contestId: string, content: string, type: ContestSorrowMessage["type"]): ContestSorrowMessage {
  const message: ContestSorrowMessage = {
    id: crypto.randomUUID(),
    contestId,
    authorName: "anonymous",
    content,
    type,
    createdAt: new Date().toISOString(),
    empathyCount: 0
  };

  db.prepare(
    "INSERT INTO sorrow_messages(id, contest_id, author_name, content, type, created_at, empathy_count) VALUES (?, ?, ?, ?, ?, ?, ?)"
  ).run(message.id, message.contestId, message.authorName, message.content, message.type, message.createdAt, message.empathyCount);

  return message;
}
