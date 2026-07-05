import express from "express";
import type { Contest, ContestWork, CreateContestRequest, UpdateContestRequest } from "@rhymers/shared";
import { db, mapContest, mapWork, type ContestRow, type ContestWorkRow } from "../db.js";

export const contestsRouter = express.Router();

contestsRouter.get("/", (_req, res) => {
  const contestRows = db.prepare("SELECT id, number, name, host_name, started_at FROM contests ORDER BY started_at DESC").all() as ContestRow[];
  const workRows = db.prepare("SELECT id, contest_id, number, title, author_name FROM contest_works").all() as ContestWorkRow[];
  const worksByContest = new Map<string, ContestWork[]>();

  for (const work of workRows) {
    const current = worksByContest.get(work.contest_id) ?? [];
    current.push(mapWork(work));
    worksByContest.set(work.contest_id, current);
  }

  res.json(contestRows.map((row) => mapContest(row, worksByContest.get(row.id) ?? [])));
});

contestsRouter.get("/:id", (req, res) => {
  const contestRow = db
    .prepare("SELECT id, number, name, host_name, started_at FROM contests WHERE id = ?")
    .get(req.params.id) as ContestRow | undefined;

  if (!contestRow) {
    res.status(404).json({ error: `Contest with ID ${req.params.id} not found` });
    return;
  }

  const works = (
    db
      .prepare("SELECT id, contest_id, number, title, author_name FROM contest_works WHERE contest_id = ? ORDER BY number ASC")
      .all(req.params.id) as ContestWorkRow[]
  ).map(mapWork);

  res.json(mapContest(contestRow, works));
});

contestsRouter.post("/", (req, res) => {
  const body = req.body as CreateContestRequest;
  if (!body?.name?.trim()) {
    res.status(400).json({ error: "Contest name is required" });
    return;
  }

  const contest: Contest = {
    id: crypto.randomUUID().replaceAll("-", ""),
    number: String((db.prepare("SELECT COUNT(1) AS count FROM contests").get() as { count: number }).count + 1).padStart(3, "0"),
    name: body.name.trim(),
    hostName: body.hostName?.trim() || "Unknown",
    startedAt: new Date().toISOString(),
    works: []
  };

  db.prepare("INSERT INTO contests(id, number, name, host_name, started_at) VALUES (?, ?, ?, ?, ?)").run(
    contest.id,
    contest.number,
    contest.name,
    contest.hostName,
    contest.startedAt
  );

  res.status(201).json(contest);
});

contestsRouter.put("/:id", (req, res) => {
  const contestRow = db
    .prepare("SELECT id, number, name, host_name, started_at FROM contests WHERE id = ?")
    .get(req.params.id) as ContestRow | undefined;

  if (!contestRow) {
    res.status(404).json({ error: `Contest with ID ${req.params.id} not found` });
    return;
  }

  const body = req.body as UpdateContestRequest;
  const newName = body.name?.trim() || contestRow.name;
  const newHost = body.hostName?.trim() || contestRow.host_name;

  db.prepare("UPDATE contests SET name = ?, host_name = ? WHERE id = ?").run(newName, newHost, req.params.id);

  const works = (
    db
      .prepare("SELECT id, contest_id, number, title, author_name FROM contest_works WHERE contest_id = ? ORDER BY number ASC")
      .all(req.params.id) as ContestWorkRow[]
  ).map(mapWork);

  res.json(
    mapContest(
      {
        ...contestRow,
        name: newName,
        host_name: newHost
      },
      works
    )
  );
});

contestsRouter.post("/:id/works", (req, res) => {
  const contestExists = db.prepare("SELECT 1 as value FROM contests WHERE id = ?").get(req.params.id) as { value: number } | undefined;
  if (!contestExists) {
    res.status(404).json({ error: `Contest with ID ${req.params.id} not found` });
    return;
  }

  const work = req.body as ContestWork;
  if (!work || typeof work.number !== "number" || !work.title?.trim()) {
    res.status(400).json({ error: "Work number and title are required" });
    return;
  }

  db.prepare("INSERT INTO contest_works(id, contest_id, number, title, author_name) VALUES (?, ?, ?, ?, ?)").run(
    crypto.randomUUID(),
    req.params.id,
    work.number,
    work.title.trim(),
    work.authorName?.trim() ?? null
  );

  const contestRow = db
    .prepare("SELECT id, number, name, host_name, started_at FROM contests WHERE id = ?")
    .get(req.params.id) as ContestRow;
  const works = (
    db
      .prepare("SELECT id, contest_id, number, title, author_name FROM contest_works WHERE contest_id = ? ORDER BY number ASC")
      .all(req.params.id) as ContestWorkRow[]
  ).map(mapWork);

  res.json(mapContest(contestRow, works));
});

contestsRouter.get("/:id/works", (req, res) => {
  const contestExists = db.prepare("SELECT 1 as value FROM contests WHERE id = ?").get(req.params.id) as { value: number } | undefined;
  if (!contestExists) {
    res.status(404).json({ error: `Contest with ID ${req.params.id} not found` });
    return;
  }

  const works = (
    db
      .prepare("SELECT id, contest_id, number, title, author_name FROM contest_works WHERE contest_id = ? ORDER BY number ASC")
      .all(req.params.id) as ContestWorkRow[]
  ).map(mapWork);

  res.json(works);
});
