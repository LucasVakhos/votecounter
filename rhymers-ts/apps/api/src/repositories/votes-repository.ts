import type { ContestResultsReport, VoteEntry } from "@rhymers/shared";
import { db, mapVote, type VoteRow } from "../db.js";

export function insertVotes(votes: VoteEntry[]): void {
  const insertVote = db.prepare("INSERT INTO votes(id, contest_id, voter_name, work_number, points) VALUES (?, ?, ?, ?, ?)");
  for (const vote of votes) {
    insertVote.run(vote.id, vote.contestId, vote.voterName, vote.workNumber, vote.points);
  }
}

export function getVotesByContest(contestId: string): VoteEntry[] {
  const rows = db
    .prepare("SELECT id, contest_id, voter_name, work_number, points FROM votes WHERE contest_id = ?")
    .all(contestId) as VoteRow[];

  return rows.map(mapVote);
}

export function getResults(contestId: string): ContestResultsReport {
  const votes = getVotesByContest(contestId);
  const byWork = new Map<number, number>();

  for (const vote of votes) {
    byWork.set(vote.workNumber, (byWork.get(vote.workNumber) ?? 0) + vote.points);
  }

  return {
    contestId,
    generatedAt: new Date().toISOString(),
    rows: [...byWork.entries()]
      .map(([workNumber, totalPoints]) => ({ workNumber, totalPoints }))
      .sort((a, b) => b.totalPoints - a.totalPoints)
  };
}
