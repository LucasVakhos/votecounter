import express from "express";
import type { ContestResultsReport, ImportResult, VoteEntry } from "@rhymers/shared";
import { getResults, getVotesByContest, insertVotes } from "../repositories/votes-repository.js";
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

  insertVotes(imported);

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
  res.json(getVotesByContest(req.params.contestId));
});

votesRouter.post("/results", (req, res) => {
  const contestId = String(req.body?.contestId ?? "").trim();
  if (!contestId) {
    res.status(400).json({ error: "Contest ID is required" });
    return;
  }

  const report: ContestResultsReport = getResults(contestId);
  res.json(report);
});
