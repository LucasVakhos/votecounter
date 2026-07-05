import express from "express";
import type { AddSorrowMessageRequest, ContestSorrowMessage } from "@rhymers/shared";
import { addSorrowMessage, getSorrowMessages } from "../repositories/sorrow-repository.js";

export const sorrowRouter = express.Router();

sorrowRouter.get("/:contestId/sorrow", (req, res) => {
  res.json(getSorrowMessages(req.params.contestId));
});

sorrowRouter.post("/:contestId/sorrow", (req, res) => {
  const { contestId } = req.params;
  const body = req.body as AddSorrowMessageRequest;

  if (!body?.content?.trim()) {
    res.status(400).json({ error: "content is required" });
    return;
  }

  const message: ContestSorrowMessage = addSorrowMessage(contestId, body.content.trim(), body.type ?? "reflection");

  res.status(201).json(message);
});
