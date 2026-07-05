import express from "express";
import type { AddSorrowMessageRequest, ContestSorrowMessage } from "@rhymers/shared";
import { db, mapSorrow, type SorrowRow } from "../db.js";

export const sorrowRouter = express.Router();

sorrowRouter.get("/:contestId/sorrow", (req, res) => {
  const rows = (
    db
      .prepare(
        "SELECT id, contest_id, author_name, content, type, created_at, empathy_count FROM sorrow_messages WHERE contest_id = ? ORDER BY created_at DESC"
      )
      .all(req.params.contestId) as SorrowRow[]
  ).map(mapSorrow);

  res.json(rows);
});

sorrowRouter.post("/:contestId/sorrow", (req, res) => {
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
