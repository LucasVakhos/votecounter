import type { ImportResult } from "@rhymers/shared";

export function parseVoteText(contestId: string, voteText: string): ImportResult {
  const lines = voteText
    .split(/\r?\n/)
    .map((x) => x.trim())
    .filter(Boolean);

  const result: ImportResult = {
    contestId,
    blocks: [],
    errors: []
  };

  for (const line of lines) {
    const [namePart, votesPart] = line.split(":");
    if (!namePart || !votesPart) {
      result.errors.push(`Invalid vote line: ${line}`);
      continue;
    }

    const voterName = namePart.trim();
    const workNumbers = votesPart
      .split(",")
      .map((x) => Number.parseInt(x.trim(), 10))
      .filter((x) => Number.isFinite(x) && x > 0);

    const entries = workNumbers.map((workNumber, idx) => ({
      workNumber,
      points: Math.max(1, 10 - idx)
    }));

    result.blocks.push({
      voterName,
      rawLine: line,
      entries
    });
  }

  return result;
}
