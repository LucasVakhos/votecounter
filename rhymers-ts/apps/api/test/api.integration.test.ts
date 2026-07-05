import test from "node:test";
import assert from "node:assert/strict";
import request from "supertest";
import { createApp } from "../src/app.js";
import { db } from "../src/db.js";

function resetDb(): void {
  db.exec(`
    DELETE FROM contest_works;
    DELETE FROM contests;
    DELETE FROM votes;
    DELETE FROM contest_comments;
    DELETE FROM work_reviews;
    DELETE FROM sorrow_messages;
  `);
}

test.beforeEach(() => {
  resetDb();
});

test("health endpoint exposes sqlite storage", async () => {
  const app = createApp();
  const res = await request(app).get("/health");

  assert.equal(res.status, 200);
  assert.equal(res.body.ok, true);
  assert.equal(res.body.storage, "sqlite");
});

test("contest create + list + works flow", async () => {
  const app = createApp();

  const created = await request(app).post("/api/contests").send({ name: "Contest A", hostName: "Host" });
  assert.equal(created.status, 201);
  assert.equal(created.body.name, "Contest A");

  const contestId = created.body.id as string;
  const addWork = await request(app).post(`/api/contests/${contestId}/works`).send({ number: 1, title: "Work 1" });
  assert.equal(addWork.status, 200);
  assert.equal(addWork.body.works.length, 1);

  const list = await request(app).get("/api/contests");
  assert.equal(list.status, 200);
  assert.equal(list.body.length, 1);
  assert.equal(list.body[0].works.length, 1);
});

test("votes import and results flow", async () => {
  const app = createApp();

  const importRes = await request(app)
    .post("/api/votes/import")
    .send({ contestId: "contest-1", voteText: "alice: 1,2\nbob: 2,1" });

  assert.equal(importRes.status, 200);
  assert.equal(importRes.body.blocks.length, 2);

  const results = await request(app).post("/api/votes/results").send({ contestId: "contest-1" });
  assert.equal(results.status, 200);
  assert.equal(results.body.rows.length, 2);
  assert.ok(results.body.rows[0].totalPoints >= results.body.rows[1].totalPoints);
});

test("discussion comments and moderation flow", async () => {
  const app = createApp();

  const created = await request(app)
    .post("/api/discussions/contests/c1/comments")
    .set("X-User-Name", "ModUser")
    .set("X-User-Role", "moderator")
    .send({ content: "Hello" });

  assert.equal(created.status, 201);
  const commentId = created.body.id as string;

  const like = await request(app).post(`/api/discussions/comments/${commentId}/like`);
  assert.equal(like.status, 200);

  const hide = await request(app)
    .post(`/api/discussions/comments/${commentId}/hide`)
    .set("X-User-Name", "ModUser")
    .set("X-User-Role", "moderator");
  assert.equal(hide.status, 200);

  const comments = await request(app).get("/api/discussions/contests/c1/comments");
  assert.equal(comments.status, 200);
  assert.equal(comments.body.length, 1);
  assert.equal(comments.body[0].isHidden, true);
});
