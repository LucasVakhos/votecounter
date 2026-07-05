import Database from "better-sqlite3";
import fs from "node:fs";
import path from "node:path";
import { dbFile, ensureDataDir } from "./db-config.js";
import {
  MIGRATIONS_SQL_DIR,
  getAppliedMigrationVersions,
  getCurrentSchemaVersion,
  getMigrations,
  migrateDown,
  runMigrations
} from "./migrations.js";

function sanitizeMigrationName(input: string): string {
  const value = input.trim().toLowerCase().replace(/\s+/g, "_").replace(/[^a-z0-9_]/g, "");
  return value || "new_migration";
}

function getNextVersion(): number {
  const all = getMigrations();
  const latest = all.at(-1);
  return latest ? latest.version + 1 : 1;
}

function createTemplate(nameArg: string | undefined, dryRun: boolean): void {
  const version = getNextVersion();
  const name = sanitizeMigrationName(nameArg ?? `migration_${version}`);
  const prefix = `v${String(version).padStart(3, "0")}_${name}`;
  const upPath = path.join(MIGRATIONS_SQL_DIR, `${prefix}.up.sql`);
  const downPath = path.join(MIGRATIONS_SQL_DIR, `${prefix}.down.sql`);

  const upTemplate = `-- Migration v${version} (${name})\n-- UP\n-- Write forward SQL here\n`;
  const downTemplate = `-- Migration v${version} (${name})\n-- DOWN\n-- Write rollback SQL here\n`;

  if (dryRun) {
    console.log(`Would create: ${upPath}`);
    console.log(upTemplate);
    console.log(`Would create: ${downPath}`);
    console.log(downTemplate);
    return;
  }

  fs.mkdirSync(MIGRATIONS_SQL_DIR, { recursive: true });
  fs.writeFileSync(upPath, upTemplate, "utf8");
  fs.writeFileSync(downPath, downTemplate, "utf8");
  console.log(`Created migration files:\n- ${upPath}\n- ${downPath}`);
  console.log("Next step: fill SQL and run migrate:up");
}

function printStatus(db: Database.Database): void {
  const all = getMigrations();
  const applied = new Set(getAppliedMigrationVersions(db));

  console.log(`DB file: ${dbFile}`);
  console.log(`Current version: ${getCurrentSchemaVersion(db)}`);
  console.log("Migrations:");

  for (const migration of all) {
    const status = applied.has(migration.version) ? "applied" : "pending";
    console.log(`  [${status}] v${migration.version} ${migration.name}`);
  }
}

function main(): void {
  ensureDataDir();
  const db = new Database(dbFile);

  const command = (process.argv[2] ?? "status").toLowerCase();

  if (command === "up") {
    const count = runMigrations(db);
    console.log(`Applied migrations: ${count}`);
    printStatus(db);
  } else if (command === "create") {
    const nameArg = process.argv[3];
    const dryRun = process.argv.includes("--dry-run");
    createTemplate(nameArg, dryRun);
  } else if (command === "down") {
    const stepsRaw = process.argv[3] ?? "1";
    const steps = Number.parseInt(stepsRaw, 10);
    if (!Number.isFinite(steps) || steps < 1) {
      throw new Error(`Invalid steps value: ${stepsRaw}`);
    }

    const reverted = migrateDown(db, steps);
    console.log(`Reverted migrations: ${reverted}`);
    printStatus(db);
  } else if (command === "status") {
    printStatus(db);
  } else {
    throw new Error(`Unknown command: ${command}. Use up | down [steps] | status | create <name> [--dry-run]`);
  }

  db.close();
}

main();
