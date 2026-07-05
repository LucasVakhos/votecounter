import cors from "cors";
import express from "express";
import { dbFile } from "./db.js";
import { contestsRouter } from "./routes/contests.js";
import { discussionsRouter } from "./routes/discussions.js";
import { sorrowRouter } from "./routes/sorrow.js";
import { votesRouter } from "./routes/votes.js";

export function createApp(): express.Express {
  const app = express();

  app.use(cors());
  app.use(express.json());

  app.get("/health", (_req, res) => {
    res.json({ ok: true, service: "rhymers-ts-api", storage: "sqlite", dbFile });
  });

  app.use("/api/contests", contestsRouter);
  app.use("/api/contests", sorrowRouter);
  app.use("/api/votes", votesRouter);
  app.use("/api/discussions", discussionsRouter);

  return app;
}
