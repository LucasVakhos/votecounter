import type { Contest, ContestWork } from "@rhymers/shared";
import { db, mapContest, mapWork, type ContestRow, type ContestWorkRow } from "../db.js";

export function getAllContests(): Contest[] {
  const contestRows = db.prepare("SELECT id, number, name, host_name, started_at FROM contests ORDER BY started_at DESC").all() as ContestRow[];
  const workRows = db.prepare("SELECT id, contest_id, number, title, author_name FROM contest_works").all() as ContestWorkRow[];
  const worksByContest = new Map<string, ContestWork[]>();

  for (const work of workRows) {
    const current = worksByContest.get(work.contest_id) ?? [];
    current.push(mapWork(work));
    worksByContest.set(work.contest_id, current);
  }

  return contestRows.map((row) => mapContest(row, worksByContest.get(row.id) ?? []));
}

export function getContestById(contestId: string): Contest | null {
  const contestRow = db
    .prepare("SELECT id, number, name, host_name, started_at FROM contests WHERE id = ?")
    .get(contestId) as ContestRow | undefined;

  if (!contestRow) {
    return null;
  }

  const works = (
    db
      .prepare("SELECT id, contest_id, number, title, author_name FROM contest_works WHERE contest_id = ? ORDER BY number ASC")
      .all(contestId) as ContestWorkRow[]
  ).map(mapWork);

  return mapContest(contestRow, works);
}

export function createContest(name: string, hostName?: string): Contest {
  const contest: Contest = {
    id: crypto.randomUUID().replaceAll("-", ""),
    number: String((db.prepare("SELECT COUNT(1) AS count FROM contests").get() as { count: number }).count + 1).padStart(3, "0"),
    name: name.trim(),
    hostName: hostName?.trim() || "Unknown",
    startedAt: new Date().toISOString(),
    works: []
  };

  db.prepare("INSERT INTO contests(id, number, name, host_name, started_at) VALUES (?, ?, ?, ?, ?)").run(
    contest.id,
    contest.number,
    contest.name,
    contest.hostName,
    contest.startedAt
  );

  return contest;
}

export function updateContest(contestId: string, name?: string, hostName?: string): Contest | null {
  const contestRow = db
    .prepare("SELECT id, number, name, host_name, started_at FROM contests WHERE id = ?")
    .get(contestId) as ContestRow | undefined;

  if (!contestRow) {
    return null;
  }

  const newName = name?.trim() || contestRow.name;
  const newHost = hostName?.trim() || contestRow.host_name;

  db.prepare("UPDATE contests SET name = ?, host_name = ? WHERE id = ?").run(newName, newHost, contestId);

  const works = (
    db
      .prepare("SELECT id, contest_id, number, title, author_name FROM contest_works WHERE contest_id = ? ORDER BY number ASC")
      .all(contestId) as ContestWorkRow[]
  ).map(mapWork);

  return mapContest({ ...contestRow, name: newName, host_name: newHost }, works);
}

export function addWork(contestId: string, work: ContestWork): Contest | null {
  const contestExists = db.prepare("SELECT 1 as value FROM contests WHERE id = ?").get(contestId) as { value: number } | undefined;
  if (!contestExists) {
    return null;
  }

  db.prepare("INSERT INTO contest_works(id, contest_id, number, title, author_name) VALUES (?, ?, ?, ?, ?)").run(
    crypto.randomUUID(),
    contestId,
    work.number,
    work.title.trim(),
    work.authorName?.trim() ?? null
  );

  return getContestById(contestId);
}

export function getWorks(contestId: string): ContestWork[] | null {
  const contestExists = db.prepare("SELECT 1 as value FROM contests WHERE id = ?").get(contestId) as { value: number } | undefined;
  if (!contestExists) {
    return null;
  }

  return (
    db
      .prepare("SELECT id, contest_id, number, title, author_name FROM contest_works WHERE contest_id = ? ORDER BY number ASC")
      .all(contestId) as ContestWorkRow[]
  ).map(mapWork);
}
