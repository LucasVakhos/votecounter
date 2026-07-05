import express from "express";
import type { ContestWork, CreateContestRequest, UpdateContestRequest } from "@rhymers/shared";
import { addWork, createContest, getAllContests, getContestById, getWorks, updateContest } from "../repositories/contests-repository.js";

export const contestsRouter = express.Router();

contestsRouter.get("/", (_req, res) => {
  res.json(getAllContests());
});

contestsRouter.get("/:id", (req, res) => {
  const contest = getContestById(req.params.id);
  if (!contest) {
    res.status(404).json({ error: `Contest with ID ${req.params.id} not found` });
    return;
  }
  res.json(contest);
});

contestsRouter.post("/", (req, res) => {
  const body = req.body as CreateContestRequest;
  if (!body?.name?.trim()) {
    res.status(400).json({ error: "Contest name is required" });
    return;
  }

  const contest = createContest(body.name, body.hostName);
  res.status(201).json(contest);
});

contestsRouter.put("/:id", (req, res) => {
  const body = req.body as UpdateContestRequest;
  const updated = updateContest(req.params.id, body.name, body.hostName);
  if (!updated) {
    res.status(404).json({ error: `Contest with ID ${req.params.id} not found` });
    return;
  }
  res.json(updated);
});

contestsRouter.post("/:id/works", (req, res) => {
  const work = req.body as ContestWork;
  if (!work || typeof work.number !== "number" || !work.title?.trim()) {
    res.status(400).json({ error: "Work number and title are required" });
    return;
  }

  const updated = addWork(req.params.id, work);
  if (!updated) {
    res.status(404).json({ error: `Contest with ID ${req.params.id} not found` });
    return;
  }
  res.json(updated);
});

contestsRouter.get("/:id/works", (req, res) => {
  const works = getWorks(req.params.id);
  if (!works) {
    res.status(404).json({ error: `Contest with ID ${req.params.id} not found` });
    return;
  }
  res.json(works);
});
