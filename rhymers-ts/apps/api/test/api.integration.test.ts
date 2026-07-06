import test from "node:test";
import assert from "node:assert/strict";
import request from "supertest";
import { createApp } from "../src/app.js";
import { db } from "../src/db.js";
import { getCurrentSchemaVersion, getMigrations, migrateDown, runMigrations } from "../src/migrations.js";

function resetDb(): void {
  db.exec(`
    DELETE FROM contest_works;
    DELETE FROM contests;
    DELETE FROM votes;
    DELETE FROM contest_comments;
    DELETE FROM work_reviews;
    DELETE FROM sorrow_messages;
    DELETE FROM moderation_actions;
  `);
}

test.beforeEach(() => {
  resetDb();
});

test("migration versions are continuous", () => {
  const migrations = getMigrations();
  assert.ok(migrations.length > 0);
  assert.equal(migrations[0].version, 1);

  for (let i = 1; i < migrations.length; i += 1) {
    assert.equal(migrations[i].version, migrations[i - 1].version + 1);
  }
});

test("migration down/up smoke test", () => {
  assert.equal(getCurrentSchemaVersion(db), 5);

  const reverted = migrateDown(db, 1);
  assert.equal(reverted, 1);
  assert.equal(getCurrentSchemaVersion(db), 4);

  const applied = runMigrations(db);
  assert.equal(applied, 1);
  assert.equal(getCurrentSchemaVersion(db), 5);

  const voteIndexes = db.prepare("PRAGMA index_list('votes')").all() as Array<{ name: string }>;
  assert.ok(voteIndexes.some((x) => x.name === "idx_votes_contest_id"));

  const commentIndexes = db.prepare("PRAGMA index_list('contest_comments')").all() as Array<{ name: string }>;
  assert.ok(commentIndexes.some((x) => x.name === "idx_contest_comments_is_deleted"));

  const reviewIndexes = db.prepare("PRAGMA index_list('work_reviews')").all() as Array<{ name: string }>;
  assert.ok(reviewIndexes.some((x) => x.name === "idx_work_reviews_is_deleted"));

  const modIndexes = db.prepare("PRAGMA index_list('moderation_actions')").all() as Array<{ name: string }>;
  assert.ok(modIndexes.some((x) => x.name === "idx_moderation_actions_target"));
});

test("health endpoint exposes sqlite storage", async () => {
  const app = createApp();
  const res = await request(app).get("/health");

  assert.equal(res.status, 200);
  assert.equal(res.body.ok, true);
  assert.equal(res.body.storage, "sqlite");
  assert.equal(res.body.schemaVersion, 5);
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
    .set("X-User-Name", "Poet")
    .set("X-User-Role", "author")
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

  const softDelete = await request(app)
    .post(`/api/discussions/comments/${commentId}/delete`)
    .set("X-User-Name", "ModUser")
    .set("X-User-Role", "moderator");
  assert.equal(softDelete.status, 200);

  const comments = await request(app).get("/api/discussions/contests/c1/comments");
  assert.equal(comments.status, 200);
  assert.equal(comments.body.length, 1);
  assert.equal(comments.body[0].isHidden, true);
  assert.equal(comments.body[0].isDeleted, true);
  assert.equal(comments.body[0].deletedBy, "ModUser");
  assert.equal(comments.body[0].content, "[Deleted by moderation]");
});

test("moderator cannot soft-delete moderator comment", async () => {
  const app = createApp();

  const created = await request(app)
    .post("/api/discussions/contests/c1/comments")
    .set("X-User-Name", "AnotherMod")
    .set("X-User-Role", "moderator")
    .send({ content: "Moderator note" });

  assert.equal(created.status, 201);
  const commentId = created.body.id as string;

  const softDelete = await request(app)
    .post(`/api/discussions/comments/${commentId}/delete`)
    .set("X-User-Name", "MainMod")
    .set("X-User-Role", "moderator");

  assert.equal(softDelete.status, 403);
  assert.equal(softDelete.body.error, "Cannot delete moderator/admin comments");
});

test("review moderation soft-delete flow", async () => {
  const app = createApp();

  const created = await request(app)
    .post("/api/discussions/contests/c1/works/1/reviews")
    .set("X-User-Name", "Poet")
    .set("X-User-Role", "author")
    .send({ title: "Strong piece", content: "Great rhythm", rating: 9 });

  assert.equal(created.status, 201);
  const reviewId = created.body.id as string;

  const softDelete = await request(app)
    .post(`/api/discussions/reviews/${reviewId}/delete`)
    .set("X-User-Name", "ModUser")
    .set("X-User-Role", "moderator");

  assert.equal(softDelete.status, 200);

  const reviews = await request(app).get("/api/discussions/contests/c1/works/1/reviews");
  assert.equal(reviews.status, 200);
  assert.equal(reviews.body.length, 1);
  assert.equal(reviews.body[0].isDeleted, true);
  assert.equal(reviews.body[0].deletedBy, "ModUser");
  assert.equal(reviews.body[0].title, "[Deleted by moderation]");
  assert.equal(reviews.body[0].content, "[Deleted by moderation]");
});

test("moderator cannot soft-delete moderator review", async () => {
  const app = createApp();

  const created = await request(app)
    .post("/api/discussions/contests/c1/works/1/reviews")
    .set("X-User-Name", "AnotherMod")
    .set("X-User-Role", "moderator")
    .send({ title: "Mod opinion", content: "Please adjust", rating: 7 });

  assert.equal(created.status, 201);
  const reviewId = created.body.id as string;

  const softDelete = await request(app)
    .post(`/api/discussions/reviews/${reviewId}/delete`)
    .set("X-User-Name", "MainMod")
    .set("X-User-Role", "moderator");

  assert.equal(softDelete.status, 403);
  assert.equal(softDelete.body.error, "Cannot delete moderator/admin reviews");
});

test("moderator can restore soft-deleted comment", async () => {
  const app = createApp();

  const created = await request(app)
    .post("/api/discussions/contests/c1/comments")
    .set("X-User-Name", "Poet")
    .set("X-User-Role", "author")
    .send({ content: "Restore me" });

  assert.equal(created.status, 201);
  const commentId = created.body.id as string;

  const del = await request(app)
    .post(`/api/discussions/comments/${commentId}/delete`)
    .set("X-User-Name", "ModUser")
    .set("X-User-Role", "moderator")
    .send({ reason: "spam" });
  assert.equal(del.status, 200);

  const restore = await request(app)
    .post(`/api/discussions/comments/${commentId}/restore`)
    .set("X-User-Name", "ModUser")
    .set("X-User-Role", "moderator");
  assert.equal(restore.status, 200);

  const comments = await request(app).get("/api/discussions/contests/c1/comments");
  assert.equal(comments.body[0].isDeleted, false);
  assert.equal(comments.body[0].content, "Restore me");
});

test("moderator can restore soft-deleted review", async () => {
  const app = createApp();

  const created = await request(app)
    .post("/api/discussions/contests/c1/works/1/reviews")
    .set("X-User-Name", "Poet")
    .set("X-User-Role", "author")
    .send({ title: "Restore me", content: "Good work", rating: 8 });

  assert.equal(created.status, 201);
  const reviewId = created.body.id as string;

  await request(app)
    .post(`/api/discussions/reviews/${reviewId}/delete`)
    .set("X-User-Name", "ModUser")
    .set("X-User-Role", "moderator")
    .send({ reason: "off-topic" });

  const restore = await request(app)
    .post(`/api/discussions/reviews/${reviewId}/restore`)
    .set("X-User-Name", "ModUser")
    .set("X-User-Role", "moderator");
  assert.equal(restore.status, 200);

  const reviews = await request(app).get("/api/discussions/contests/c1/works/1/reviews");
  assert.equal(reviews.body[0].isDeleted, false);
  assert.equal(reviews.body[0].title, "Restore me");
});

test("moderation deleted list and log endpoints", async () => {
  const app = createApp();

  // create and delete a comment
  const c = await request(app)
    .post("/api/discussions/contests/c2/comments")
    .set("X-User-Name", "Troll")
    .set("X-User-Role", "author")
    .send({ content: "Bad post" });
  const commentId = c.body.id as string;

  await request(app)
    .post(`/api/discussions/comments/${commentId}/delete`)
    .set("X-User-Name", "ModUser")
    .set("X-User-Role", "moderator")
    .send({ reason: "abuse" });

  // list deleted items
  const deleted = await request(app)
    .get("/api/discussions/moderation/deleted?contestId=c2")
    .set("X-User-Name", "ModUser")
    .set("X-User-Role", "moderator");
  assert.equal(deleted.status, 200);
  assert.equal(deleted.body.length, 1);
  assert.equal(deleted.body[0].reason, "abuse");
  assert.equal(deleted.body[0].originalContent, "Bad post");
  assert.equal(deleted.body[0].targetType, "comment");

  // moderation log
  const log = await request(app)
    .get("/api/discussions/moderation/log")
    .set("X-User-Name", "ModUser")
    .set("X-User-Role", "moderator");
  assert.equal(log.status, 200);
  assert.ok(log.body.length >= 1);
  const entry = log.body[0];
  assert.equal(entry.action, "delete");
  assert.equal(entry.moderatorName, "ModUser");
  assert.equal(entry.reason, "abuse");

  // non-moderator cannot access deleted list
  const forbidden = await request(app)
    .get("/api/discussions/moderation/deleted")
    .set("X-User-Name", "SomeReader")
    .set("X-User-Role", "reader");
  assert.equal(forbidden.status, 403);
});
