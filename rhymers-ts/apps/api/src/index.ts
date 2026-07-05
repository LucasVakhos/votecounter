import cors from "cors";
import express from "express";
import { dbFile } from "./db.js";
import { contestsRouter } from "./routes/contests.js";
import { discussionsRouter } from "./routes/discussions.js";
import { sorrowRouter } from "./routes/sorrow.js";
import { votesRouter } from "./routes/votes.js";

const app = express();
const port = Number(process.env.PORT ?? 4000);

app.use(cors());
app.use(express.json());

app.get("/health", (_req, res) => {
  res.json({ ok: true, service: "rhymers-ts-api", storage: "sqlite", dbFile });
});

app.use("/api/contests", contestsRouter);
app.use("/api/contests", sorrowRouter);
app.use("/api/votes", votesRouter);
app.use("/api/discussions", discussionsRouter);

app.listen(port, () => {
  console.log(`rhymers-ts api listening on http://localhost:${port}`);
});
