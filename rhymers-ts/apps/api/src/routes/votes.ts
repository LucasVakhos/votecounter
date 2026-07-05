import express from "express";
import type { ContestResultsReport, ImportResult, VoteEntry } from "@rhymers/shared";
import { db, mapVote, type VoteRow } from "../db.js";
import { parseVoteText } from "../vote-parser.js";

export const votesRouter = express.Router();

votesRouter.post("/import", (req, res) => {
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

votesRouter.post("/validate", (req, res) => {
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

votesRouter.get("/contest/:contestId", (req, res) => {
  const rows = db
    .prepare("SELECT id, contest_id, voter_name, work_number, points FROM votes WHERE contest_id = ?")
    .all(req.params.contestId) as VoteRow[];

  res.json(rows.map(mapVote));
});

votesRouter.post("/results", (req, res) => {
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
