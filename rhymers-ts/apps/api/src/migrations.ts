import type Database from "better-sqlite3";

export type Migration = {
  version: number;
  name: string;
  up: string;
  down: string;
};

const migrations: Migration[] = [
  {
    version: 1,
    name: "init_schema",
    up: `
CREATE TABLE IF NOT EXISTS contests (
  id TEXT PRIMARY KEY,
  number TEXT NOT NULL,
  name TEXT NOT NULL,
  host_name TEXT NOT NULL,
  started_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS contest_works (
  id TEXT PRIMARY KEY,
  contest_id TEXT NOT NULL,
  number INTEGER NOT NULL,
  title TEXT NOT NULL,
  author_name TEXT,
  FOREIGN KEY(contest_id) REFERENCES contests(id)
);

CREATE TABLE IF NOT EXISTS votes (
  id TEXT PRIMARY KEY,
  contest_id TEXT NOT NULL,
  voter_name TEXT NOT NULL,
  work_number INTEGER NOT NULL,
  points INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS contest_comments (
  id TEXT PRIMARY KEY,
  contest_id TEXT NOT NULL,
  author_name TEXT NOT NULL,
  author_role TEXT NOT NULL,
  content TEXT NOT NULL,
  parent_comment_id TEXT,
  likes_count INTEGER NOT NULL,
  is_approved INTEGER NOT NULL,
  is_hidden INTEGER NOT NULL,
  created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS work_reviews (
  id TEXT PRIMARY KEY,
  contest_id TEXT NOT NULL,
  work_number INTEGER NOT NULL,
  work_title TEXT NOT NULL,
  reviewer_name TEXT NOT NULL,
  reviewer_role TEXT NOT NULL,
  title TEXT NOT NULL,
  content TEXT NOT NULL,
  rating INTEGER,
  strengths TEXT,
  areas_for_improvement TEXT,
  author_response TEXT,
  helpful_count INTEGER NOT NULL,
  is_approved INTEGER NOT NULL,
  is_hidden INTEGER NOT NULL,
  created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS sorrow_messages (
  id TEXT PRIMARY KEY,
  contest_id TEXT NOT NULL,
  author_name TEXT NOT NULL,
  content TEXT NOT NULL,
  type TEXT NOT NULL,
  created_at TEXT NOT NULL,
  empathy_count INTEGER NOT NULL
);
`,
    down: `
DROP TABLE IF EXISTS sorrow_messages;
DROP TABLE IF EXISTS work_reviews;
DROP TABLE IF EXISTS contest_comments;
DROP TABLE IF EXISTS votes;
DROP TABLE IF EXISTS contest_works;
DROP TABLE IF EXISTS contests;
`
  },
  {
    version: 2,
    name: "add_indexes",
    up: `
CREATE INDEX IF NOT EXISTS idx_contest_works_contest_id ON contest_works(contest_id);
CREATE INDEX IF NOT EXISTS idx_votes_contest_id ON votes(contest_id);
CREATE INDEX IF NOT EXISTS idx_contest_comments_contest_id ON contest_comments(contest_id);
CREATE INDEX IF NOT EXISTS idx_work_reviews_contest_work ON work_reviews(contest_id, work_number);
CREATE INDEX IF NOT EXISTS idx_sorrow_messages_contest_id ON sorrow_messages(contest_id);
`,
    down: `
DROP INDEX IF EXISTS idx_sorrow_messages_contest_id;
DROP INDEX IF EXISTS idx_work_reviews_contest_work;
DROP INDEX IF EXISTS idx_contest_comments_contest_id;
DROP INDEX IF EXISTS idx_votes_contest_id;
DROP INDEX IF EXISTS idx_contest_works_contest_id;
`
  }
];

function ensureMigrationsTable(db: Database.Database): void {
  db.exec(`
CREATE TABLE IF NOT EXISTS schema_migrations (
  version INTEGER PRIMARY KEY,
  name TEXT NOT NULL,
  applied_at TEXT NOT NULL
);
`);
}

export function runMigrations(db: Database.Database): number {
  ensureMigrationsTable(db);

  const applied = new Set<number>(
    (db.prepare("SELECT version FROM schema_migrations ORDER BY version ASC").all() as Array<{ version: number }>).map((x) => x.version)
  );

  let appliedCount = 0;
  const insert = db.prepare("INSERT INTO schema_migrations(version, name, applied_at) VALUES (?, ?, ?)");

  for (const migration of migrations.sort((a, b) => a.version - b.version)) {
    if (applied.has(migration.version)) {
      continue;
    }

    const tx = db.transaction(() => {
      db.exec(migration.up);
      insert.run(migration.version, migration.name, new Date().toISOString());
    });

    tx();
    appliedCount += 1;
  }

  return appliedCount;
}

export function getMigrations(): Migration[] {
  return [...migrations].sort((a, b) => a.version - b.version);
}

export function getAppliedMigrationVersions(db: Database.Database): number[] {
  ensureMigrationsTable(db);
  return (db.prepare("SELECT version FROM schema_migrations ORDER BY version ASC").all() as Array<{ version: number }>).map((x) => x.version);
}

export function migrateDown(db: Database.Database, steps = 1): number {
  ensureMigrationsTable(db);
  if (steps <= 0) {
    return 0;
  }

  const applied = getAppliedMigrationVersions(db);
  const candidates = applied.slice(-steps).reverse();
  const byVersion = new Map(getMigrations().map((m) => [m.version, m]));
  const remove = db.prepare("DELETE FROM schema_migrations WHERE version = ?");

  let reverted = 0;
  for (const version of candidates) {
    const migration = byVersion.get(version);
    if (!migration) {
      throw new Error(`Missing migration definition for version ${version}`);
    }

    const tx = db.transaction(() => {
      db.exec(migration.down);
      remove.run(migration.version);
    });

    tx();
    reverted += 1;
  }

  return reverted;
}

export function getCurrentSchemaVersion(db: Database.Database): number {
  ensureMigrationsTable(db);
  const row = db.prepare("SELECT MAX(version) as version FROM schema_migrations").get() as { version: number | null };
  return row.version ?? 0;
}
