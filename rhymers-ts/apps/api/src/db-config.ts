import fs from "node:fs";
import path from "node:path";

export const dataDir = path.resolve(process.cwd(), "data");
export const dbFile = path.join(dataDir, "rhymers.db");

export function ensureDataDir(): void {
  fs.mkdirSync(dataDir, { recursive: true });
}
