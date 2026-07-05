import cors from "cors";
import express from "express";
import type { AddSorrowMessageRequest, ContestSorrowMessage } from "@rhymers/shared";

const app = express();
const port = Number(process.env.PORT ?? 4000);

app.use(cors());
app.use(express.json());

const sorrowMessages: ContestSorrowMessage[] = [];

app.get("/health", (_req, res) => {
  res.json({ ok: true, service: "rhymers-ts-api" });
});

app.get("/api/contests/:contestId/sorrow", (req, res) => {
  const { contestId } = req.params;
  res.json(sorrowMessages.filter((x) => x.contestId === contestId));
});

app.post("/api/contests/:contestId/sorrow", (req, res) => {
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

  sorrowMessages.unshift(message);
  res.status(201).json(message);
});

app.listen(port, () => {
  console.log(`rhymers-ts api listening on http://localhost:${port}`);
});
