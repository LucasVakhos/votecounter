import type Database from "better-sqlite3";
import fs from "node:fs";
import path from "node:path";

export type Migration = {
  version: number;
  name: string;
  up: string;
  down: string;
};

export type MigrationVerification = {
  count: number;
  firstVersion: number;
  lastVersion: number;
};

export const MIGRATIONS_SQL_DIR = path.resolve(process.cwd(), "src", "migrations", "sql");

function loadMigrations(): Migration[] {
  if (!fs.existsSync(MIGRATIONS_SQL_DIR)) {
    return [];
  }

  const files = fs.readdirSync(MIGRATIONS_SQL_DIR);
  const re = /^v(\d+)_([a-z0-9_]+)\.(up|down)\.sql$/;
  const grouped = new Map<number, { name: string; up?: string; down?: string }>();

  for (const fileName of files) {
    const match = fileName.match(re);
    if (!match) {
      continue;
    }

    const versionToken = match[1];
    const nameToken = match[2];
    const directionToken = match[3];
    if (!versionToken || !nameToken || !directionToken) {
      continue;
    }

    const version = Number.parseInt(versionToken, 10);
    const name = nameToken;
    const dir = directionToken;
    const content = fs.readFileSync(path.join(MIGRATIONS_SQL_DIR, fileName), "utf8");

    const existing = grouped.get(version) ?? { name };
    if (existing.name !== name) {
      throw new Error(`Mismatched migration names for version ${version}: '${existing.name}' vs '${name}'`);
    }

    if (dir === "up") {
      existing.up = content;
    } else {
      existing.down = content;
    }

    grouped.set(version, existing);
  }

  const migrations: Migration[] = [];
  for (const [version, value] of grouped.entries()) {
    if (!value.up || !value.down) {
      throw new Error(`Migration v${version}_${value.name} must have both .up.sql and .down.sql files`);
    }

    migrations.push({
      version,
      name: value.name,
      up: value.up,
      down: value.down
    });
  }

  const sorted = migrations.sort((a, b) => a.version - b.version);
  if (sorted.length === 0) {
    return sorted;
  }

  const first = sorted.at(0);
  if (!first) {
    return sorted;
  }

  if (first.version !== 1) {
    throw new Error(`Migration chain must start at version 1, got v${first.version}`);
  }

  for (let i = 1; i < sorted.length; i += 1) {
    const prev = sorted.at(i - 1);
    const curr = sorted.at(i);
    if (!prev || !curr) {
      continue;
    }

    if (curr.version !== prev.version + 1) {
      throw new Error(`Migration versions must be continuous. Gap detected between v${prev.version} and v${curr.version}`);
    }
  }

  return sorted;
}

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
  const migrations = loadMigrations();

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
  return loadMigrations();
}

export function verifyMigrations(): MigrationVerification {
  const migrations = loadMigrations();
  if (migrations.length === 0) {
    throw new Error(`No migrations found in ${MIGRATIONS_SQL_DIR}`);
  }

  const first = migrations.at(0);
  const last = migrations.at(-1);
  if (!first || !last) {
    throw new Error(`No migrations found in ${MIGRATIONS_SQL_DIR}`);
  }

  return {
    count: migrations.length,
    firstVersion: first.version,
    lastVersion: last.version
  };
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
