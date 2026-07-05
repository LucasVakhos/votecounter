import Database from "better-sqlite3";
import { dbFile, ensureDataDir } from "./db-config.js";
import {
  getAppliedMigrationVersions,
  getCurrentSchemaVersion,
  getMigrations,
  migrateDown,
  runMigrations
} from "./migrations.js";

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
    throw new Error(`Unknown command: ${command}. Use up | down [steps] | status`);
  }

  db.close();
}

main();
