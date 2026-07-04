const STORAGE_KEY = "votecounter.html.state.v1";
const UNKNOWN_AUTHOR = "Неизвестный автор";

const state = loadState();
let currentContestId = state.currentContestId || state.contests[0]?.id;
let lastPreview = { blocks: [], warnings: [] };

const $ = (id) => document.getElementById(id);
const viewButtons = [...document.querySelectorAll(".nav-item")];

function uid() {
  return Math.random().toString(16).slice(2) + Date.now().toString(16);
}

function defaultContest() {
  return {
    id: uid(),
    number: "001",
    name: "Новый конкурс",
    stage: "Приём работ",
    hostName: "",
    nextHostName: "",
    voteLimit: 0,
    baseVote: 3,
    maxVote: 4,
    limitMaxVote: 0,
    limitMaxVoteByTopic: false,
    oneMaxVotePerTopic: false,
    downgradeExtraMaxVote: true,
    allowZeroVotes: false,
    treatSelfVoteAsZero: true,
    hostKnowsAuthors: true,
    works: [],
    votes: [],
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString()
  };
}

function loadState() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw);
      if (Array.isArray(parsed.contests) && parsed.contests.length) return parsed;
    }
  } catch {
    localStorage.removeItem(STORAGE_KEY);
  }

  const first = defaultContest();
  first.works = [
    { number: 1, title: "Первая работа", author: UNKNOWN_AUTHOR, topic: "", content: "" },
    { number: 2, title: "Вторая работа", author: UNKNOWN_AUTHOR, topic: "", content: "" }
  ];
  return { contests: [first], currentContestId: first.id };
}

function saveState() {
  state.currentContestId = currentContestId;
  localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
}

function currentContest() {
  let contest = state.contests.find((item) => item.id === currentContestId);
  if (!contest) {
    contest = state.contests[0] || defaultContest();
    if (!state.contests.length) state.contests.push(contest);
    currentContestId = contest.id;
  }
  contest.works ||= [];
  contest.votes ||= [];
  return contest;
}

function normalizeName(value) {
  return (value || "")
    .toString()
    .trim()
    .toLocaleLowerCase("ru-RU")
    .replace(/ё/g, "е")
    .replace(/\s+/g, " ");
}

function sameName(a, b) {
  return normalizeName(a) && normalizeName(a) === normalizeName(b);
}

function numberText(value) {
  return String(value).padStart(2, "0");
}

function scoreText(value) {
  const n = Number(value || 0);
  return Number.isInteger(n) ? String(n) : String(n).replace(".", ",");
}

function parseScore(raw) {
  const text = String(raw || "").trim().replace(",", ".");
  const match = text.match(/^([0-9]+(?:\.[0-9]+)?)(\s*[+\-−])?/);
  if (!match) return null;
  let score = Number(match[1]);
  let label = match[1].replace(".", ",");
  if (match[2] && match[2].includes("+")) {
    score += 0.5;
    label = `${match[1]}+`;
  }
  if (match[2] && (match[2].includes("-") || match[2].includes("−"))) {
    score -= 0.5;
    label = `${match[1]}-`;
  }
  return Number.isFinite(score) ? { score, text: label } : null;
}

function parseVotePairs(line, allowZeroVotes) {
  const pairs = [];
  const regex = /(?:№\s*)?(\d{1,3})\s*(?:[-–—:=]\s*|\s+)([0-9]+(?:[,.][0-9]+)?\s*[+\-−]?)(?:\s*\(([^)]*)\))?/g;
  let match;
  while ((match = regex.exec(line))) {
    const score = parseScore(match[2]);
    if (!score) continue;
    if (score.score === 0 && !allowZeroVotes) {
      pairs.push({ workNo: Number(match[1]), score: 0, scoreText: "0", comment: match[3] || "", sourceLine: line });
      continue;
    }
    pairs.push({ workNo: Number(match[1]), score: score.score, scoreText: score.text, comment: match[3] || "", sourceLine: line });
  }
  return pairs;
}

function looksLikeVoter(line) {
  const value = line.trim();
  if (!value || value.length > 80 || value.endsWith(":")) return false;
  if (/[,";!?…«»"“”]/.test(value)) return false;
  if (/[-–—]/.test(value)) return false;
  const words = value.split(/\s+/).filter((part) => /[A-Za-zА-Яа-яЁё]/.test(part));
  return words.length >= 1 && words.length <= 6;
}

function shouldSkipVoteLine(line) {
  const value = line.trim();
  if (!value) return false;
  if (value === "Ответить") return true;
  if (/^\d+$/.test(value)) return true;
  if (/^(вчера|сегодня)?\s*\d{1,2}:\d{2}$/i.test(value)) return true;
  if (/^\d{1,2}\s+(янв|фев|мар|апр|ма[йя]|июн|июл|авг|сен|сент|окт|ноя|дек)/i.test(value)) return true;
  if (/^[-–—\s]+$/.test(value)) return true;
  return false;
}

function parseVotes(text, contest) {
  const result = { blocks: [], warnings: [] };
  let candidate = "";
  let current = { voterName: "", votes: [] };
  const lines = String(text || "").replace(/\r\n/g, "\n").replace(/\r/g, "\n").split("\n");

  for (const [index, raw] of lines.entries()) {
    const line = raw.trim();
    if (shouldSkipVoteLine(line)) {
      if (current.votes.length) {
        finishVoteBlock(result, current, contest);
        current = { voterName: "", votes: [] };
      }
      candidate = "";
      continue;
    }

    const pairs = parseVotePairs(line, contest.allowZeroVotes);
    if (pairs.length) {
      if (!candidate) {
        result.warnings.push(`Строка ${index + 1}: оценка без имени голосующего пропущена.`);
        continue;
      }
      current.voterName = candidate;
      for (const pair of pairs) {
        current.votes.push({
          id: uid(),
          contestId: contest.id,
          voterName: candidate,
          voterKey: normalizeName(candidate),
          workNo: pair.workNo,
          score: pair.score,
          scoreText: pair.scoreText,
          originalScore: pair.score,
          originalScoreText: pair.scoreText,
          votedScore: pair.score,
          votedScoreText: pair.scoreText,
          acceptedScore: pair.score,
          acceptedScoreText: pair.scoreText,
          wasChangedByRules: false,
          ruleNote: "",
          comment: pair.comment,
          sourceLine: pair.sourceLine,
          updatedAt: new Date().toISOString()
        });
      }
      continue;
    }

    if (current.votes.length) {
      if (looksLikeVoter(line)) {
        finishVoteBlock(result, current, contest);
        current = { voterName: "", votes: [] };
        candidate = line;
      }
      continue;
    }

    if (looksLikeVoter(line)) candidate = line;
  }

  if (current.votes.length) finishVoteBlock(result, current, contest);
  validateVoteResult(result, contest);
  applyRules(contest, result);
  return result;
}

function finishVoteBlock(result, block, contest) {
  const byWork = new Map();
  for (const vote of block.votes) byWork.set(vote.workNo, vote);
  let compact = [...byWork.values()].sort((a, b) => a.workNo - b.workNo);
  if (contest.voteLimit > 0 && compact.length > contest.voteLimit) compact = compact.slice(0, contest.voteLimit);
  result.blocks.push({ voterName: block.voterName, votes: compact });
}

function validateVoteResult(result, contest) {
  const known = new Set(contest.works.map((work) => Number(work.number)).filter(Boolean));
  if (!known.size) return;
  for (const block of result.blocks) {
    const voted = new Set(block.votes.map((vote) => vote.workNo));
    const unknown = [...voted].filter((no) => !known.has(no)).sort((a, b) => a - b);
    const missing = [...known].filter((no) => !voted.has(no)).sort((a, b) => a - b);
    if (unknown.length) result.warnings.push(`${block.voterName}: номера не найдены: ${unknown.map(numberText).join(", ")}.`);
    if (missing.length) result.warnings.push(`${block.voterName}: нет оценок для работ: ${missing.map(numberText).join(", ")}.`);
  }
}

function applyRules(contest, result) {
  const works = new Map(contest.works.map((work) => [Number(work.number), work]));
  for (const block of result.blocks) {
    for (const vote of block.votes) {
      const work = works.get(vote.workNo);
      if (contest.treatSelfVoteAsZero && work && sameName(block.voterName, work.author)) {
        changeScore(vote, 0, "0", "самоголосование = 0");
      }
      if (vote.score > Number(contest.maxVote)) {
        const next = Math.min(Number(contest.baseVote), Number(contest.maxVote));
        changeScore(vote, next, scoreText(next), `оценка выше максимальной ${contest.maxVote}`);
      }
      if (vote.score === 0 && !contest.allowZeroVotes && !vote.ruleNote.toLowerCase().includes("самоголос")) {
        const next = Math.max(1, Math.min(Number(contest.baseVote), Number(contest.maxVote)));
        changeScore(vote, next, scoreText(next), `0 не разрешён`);
      }
    }
    applyMaxLimits(contest, works, block, result.warnings);
    for (const vote of block.votes) {
      vote.acceptedScore = vote.score;
      vote.acceptedScoreText = vote.scoreText;
    }
  }
}

function applyMaxLimits(contest, works, block, warnings) {
  const limit = contest.oneMaxVotePerTopic ? 1 : Number(contest.limitMaxVote || 0);
  if (limit <= 0) return;
  const groups = new Map();
  for (const vote of block.votes) {
    let key = "__all__";
    if (contest.limitMaxVoteByTopic || contest.oneMaxVotePerTopic) {
      key = (works.get(vote.workNo)?.topic || "Общая тема").trim();
    }
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(vote);
  }
  for (const [key, votes] of groups) {
    const maxVotes = votes.filter((vote) => Number(vote.score) === Number(contest.maxVote));
    if (maxVotes.length <= limit) continue;
    warnings.push(`${block.voterName}: максимумов ${maxVotes.length}, разрешено ${limit}. Лишние исправлены.`);
    for (const vote of maxVotes.slice(limit)) {
      if (contest.downgradeExtraMaxVote) {
        const next = Math.min(Number(contest.baseVote), Number(contest.maxVote));
        changeScore(vote, next, scoreText(next), `лимит максимумов исчерпан${key === "__all__" ? "" : `, тема ${key}`}`);
      } else {
        changeScore(vote, 0, "0", "лимит максимумов исчерпан");
      }
    }
  }
}

function changeScore(vote, score, text, note) {
  if (vote.score !== score || vote.scoreText !== text) vote.wasChangedByRules = true;
  vote.score = score;
  vote.scoreText = text;
  vote.acceptedScore = score;
  vote.acceptedScoreText = text;
  vote.ruleNote = vote.ruleNote ? `${vote.ruleNote}; ${note}` : note;
}

function parseWorks(text, mode) {
  const works = [];
  const byNo = new Map();
  const warnings = [];
  const lines = String(text || "").replace(/\r\n/g, "\n").replace(/\r/g, "\n").split("\n").map((line) => line.trim()).filter(Boolean);
  let topic = "";

  const upsert = (work) => {
    if (!work.number) return;
    if (byNo.has(work.number)) warnings.push(`Повтор номера ${numberText(work.number)}: оставлена последняя версия.`);
    byNo.set(work.number, { ...byNo.get(work.number), ...work });
  };

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    if (line === "." || line === "Ответить") continue;
    if (/^["«“]/.test(line)) {
      topic = cleanWorkText(line);
      continue;
    }

    const numberOnly = line.match(/^(?:№\s*)?(\d{1,3})$/i);
    if (numberOnly) {
      const number = Number(numberOnly[1]);
      const title = cleanWorkText(lines[i + 1] || "");
      const authorLine = cleanWorkText(lines[i + 2] || "");
      if (title) {
        upsert({
          number,
          title: mode === "authors" ? "" : title,
          author: mode === "hidden" ? UNKNOWN_AUTHOR : (authorLine || UNKNOWN_AUTHOR),
          topic,
          content: ""
        });
        i += authorLine ? 2 : 1;
      }
      continue;
    }

    const match = line.match(/^(?:№\s*)?(\d{1,3})(?:\s*[\.)\:]\s*|\s*[-–—]\s*|\s+)(.+)$/i);
    if (!match) continue;
    const number = Number(match[1]);
    const tail = cleanWorkText(match[2]);
    if (/^[0-4]\s*[+\-−]?$/.test(tail)) {
      upsert({ number, title: topic, author: UNKNOWN_AUTHOR, topic, content: "" });
      continue;
    }
    const parts = tail.split(/\s+[-–—/]\s+/).map(cleanWorkText).filter(Boolean);
    if (mode === "authors") {
      upsert({ number, title: "", author: cleanWorkText(parts.at(-1) || tail), topic });
    } else {
      upsert({
        number,
        title: parts[0] || tail,
        author: mode === "hidden" ? UNKNOWN_AUTHOR : (parts.slice(1).join(" - ") || UNKNOWN_AUTHOR),
        topic,
        content: ""
      });
    }
  }

  works.push(...[...byNo.values()].sort((a, b) => a.number - b.number));
  return { works, warnings };
}

function cleanWorkText(value) {
  return String(value || "").trim().replace(/^["«“„]+|["»”]+$/g, "").replace(/\s+/g, " ").trim();
}

function buildResults(contest) {
  const workByNo = new Map(contest.works.map((work) => [Number(work.number), work]));
  const lastVotes = new Map();
  for (const vote of contest.votes) {
    if (!workByNo.has(Number(vote.workNo))) continue;
    lastVotes.set(`${vote.voterKey}|${vote.workNo}`, vote);
  }
  const votes = [...lastVotes.values()];
  const voterCount = new Set(votes.map((vote) => vote.voterKey).filter(Boolean)).size;
  const rows = contest.works.map((work) => {
    const workVotes = votes.filter((vote) => Number(vote.workNo) === Number(work.number));
    const accepted = workVotes.filter((vote) => !(contest.treatSelfVoteAsZero && sameName(vote.voterName, work.author)));
    const rate = accepted.reduce((sum, vote) => sum + Number(vote.score || 0), 0);
    return {
      workNo: Number(work.number),
      title: work.title || "",
      author: work.author || "",
      topic: work.topic || "",
      rate,
      acceptedVotes: accepted.length,
      selfVotes: workVotes.length - accepted.length,
      maxVotes: accepted.filter((vote) => Number(vote.score) === Number(contest.maxVote)).length,
      average: accepted.length ? rate / accepted.length : 0,
      placeNo: 0,
      placeTo: 0
    };
  }).sort((a, b) => b.rate - a.rate || a.workNo - b.workNo);

  let index = 0;
  for (const rate of [...new Set(rows.map((row) => row.rate))].sort((a, b) => b - a)) {
    const group = rows.filter((row) => row.rate === rate);
    for (const row of group) {
      row.placeNo = index + 1;
      row.placeTo = index + group.length;
    }
    index += group.length;
  }
  rows.sort((a, b) => a.placeNo - b.placeNo || a.workNo - b.workNo);
  return { rows, voterCount, acceptedVoteCount: rows.reduce((sum, row) => sum + row.acceptedVotes, 0) };
}

function placeText(row) {
  return row.placeNo === row.placeTo ? String(row.placeNo) : `${row.placeNo}-${row.placeTo}`;
}

function buildFinalText(contest, report) {
  const lines = [];
  lines.push(`ИТОГИ ${contest.number ? `№${contest.number}: ` : ""}${contest.name || "Конкурс"}`);
  lines.push("------------------------");
  lines.push(`Проголосовало судей: ${report.voterCount}`);
  lines.push(`Работ в конкурсе: ${contest.works.length}`);
  lines.push(`Принято голосов: ${report.acceptedVoteCount}`);
  lines.push("");
  lines.push("Рейтинг:");
  for (const row of report.rows) {
    lines.push(`${placeText(row)}. №${numberText(row.workNo)} - ${row.author || "автор не указан"} - "${row.title || "без названия"}" - ${scoreText(row.rate)}`);
  }
  const winners = report.rows.filter((row) => row.placeNo <= 3);
  if (winners.length) {
    lines.push("");
    lines.push("Победители:");
    for (const row of winners) lines.push(`${placeText(row)} место - №${numberText(row.workNo)}, ${row.author || "автор не указан"}, ${scoreText(row.rate)}`);
  }
  return lines.join("\n");
}

function renderAll() {
  const contest = currentContest();
  renderContestSelect();
  renderContestList();
  renderContestCard();
  renderSettings();
  renderWorks();
  renderPreview(lastPreview);
  renderControl();
  renderResults();
  renderChanges();
  renderPeople();
}

function renderContestSelect() {
  $("contestSelect").innerHTML = state.contests.map((contest) => `<option value="${contest.id}">${escapeHtml(contest.number)} - ${escapeHtml(contest.name)}</option>`).join("");
  $("contestSelect").value = currentContestId;
}

function renderContestList() {
  $("contestRows").innerHTML = state.contests.map((contest) => {
    const report = buildResults(contest);
    return `<tr class="clickable ${contest.id === currentContestId ? "selected" : ""}" data-contest-id="${contest.id}">
      <td>${escapeHtml(contest.number)}</td>
      <td>${escapeHtml(contest.name)}</td>
      <td>${contest.works.length}</td>
      <td>${report.voterCount}</td>
      <td>${contest.votes.length}</td>
      <td>${new Date(contest.updatedAt || contest.createdAt).toLocaleString("ru-RU")}</td>
    </tr>`;
  }).join("");
}

function renderContestCard() {
  const contest = currentContest();
  const report = buildResults(contest);
  const changes = contest.votes.filter((vote) => vote.wasChangedByRules).length;
  $("contestCard").innerHTML = [
    ["Название", contest.name],
    ["Работ", contest.works.length],
    ["Голосующих", report.voterCount],
    ["Принято голосов", report.acceptedVoteCount],
    ["Правок правил", changes],
    ["Режим", contest.hostKnowsAuthors ? "ведущий знает авторов" : "авторы скрыты"]
  ].map(([label, value]) => `<div class="metric"><span>${label}</span><strong>${escapeHtml(value)}</strong></div>`).join("");
}

function renderSettings() {
  const contest = currentContest();
  $("contestNumber").value = contest.number || "";
  $("contestName").value = contest.name || "";
  $("hostName").value = contest.hostName || "";
  $("nextHostName").value = contest.nextHostName || "";
  $("voteLimit").value = contest.voteLimit || 0;
  $("baseVote").value = contest.baseVote ?? 3;
  $("maxVote").value = contest.maxVote ?? 4;
  $("limitMaxVote").value = contest.limitMaxVote || 0;
  $("hostKnowsAuthors").checked = !!contest.hostKnowsAuthors;
  $("limitMaxVoteByTopic").checked = !!contest.limitMaxVoteByTopic;
  $("oneMaxVotePerTopic").checked = !!contest.oneMaxVotePerTopic;
  $("downgradeExtraMaxVote").checked = !!contest.downgradeExtraMaxVote;
  $("allowZeroVotes").checked = !!contest.allowZeroVotes;
  $("selfVoteZero").checked = !!contest.treatSelfVoteAsZero;
}

function renderWorks() {
  const contest = currentContest();
  $("workRows").innerHTML = contest.works
    .slice()
    .sort((a, b) => Number(a.number) - Number(b.number))
    .map((work) => `<tr>
      <td>${numberText(work.number)}</td>
      <td>${escapeHtml(work.title)}</td>
      <td>${escapeHtml(work.author)}</td>
      <td>${escapeHtml(work.topic || "")}</td>
    </tr>`)
    .join("");
}

function renderPreview(preview) {
  $("voteWarnings").innerHTML = (preview.warnings || []).map((warning) => `<div class="warning">${escapeHtml(warning)}</div>`).join("");
  $("votePreviewRows").innerHTML = (preview.blocks || []).flatMap((block) => block.votes).map((vote) => `<tr>
    <td>${escapeHtml(vote.voterName)}</td>
    <td>${numberText(vote.workNo)}</td>
    <td>${escapeHtml(vote.originalScoreText)}</td>
    <td>${escapeHtml(vote.scoreText)}</td>
    <td>${escapeHtml(vote.ruleNote)}</td>
  </tr>`).join("");
}

function renderControl() {
  const contest = currentContest();
  const known = new Set(contest.works.map((work) => Number(work.number)));
  const voters = new Map();
  for (const vote of contest.votes) {
    if (!voters.has(vote.voterKey)) voters.set(vote.voterKey, { name: vote.voterName, votes: [] });
    voters.get(vote.voterKey).votes.push(vote);
  }
  $("controlRows").innerHTML = [...voters.values()].sort((a, b) => a.name.localeCompare(b.name, "ru")).map((row) => {
    const voted = new Set(row.votes.map((vote) => Number(vote.workNo)));
    const missing = [...known].filter((no) => !voted.has(no));
    const unknown = [...voted].filter((no) => !known.has(no));
    const ok = !missing.length && !unknown.length;
    return `<tr>
      <td>${escapeHtml(row.name)}</td>
      <td>${ok ? "принято" : "проверить"}</td>
      <td>${voted.size}</td>
      <td>${missing.map(numberText).join(", ")}</td>
      <td>${unknown.map(numberText).join(", ")}</td>
    </tr>`;
  }).join("");
}

function renderResults() {
  const contest = currentContest();
  const report = buildResults(contest);
  $("resultRows").innerHTML = report.rows.map((row) => `<tr>
    <td>${placeText(row)}</td>
    <td>${numberText(row.workNo)}</td>
    <td>${escapeHtml(row.title)}</td>
    <td>${escapeHtml(row.author)}</td>
    <td>${scoreText(row.rate)}</td>
    <td>${row.acceptedVotes}</td>
    <td>${scoreText(row.average)}</td>
  </tr>`).join("");
  $("finalText").value = buildFinalText(contest, report);
}

function renderChanges() {
  const contest = currentContest();
  $("changeRows").innerHTML = contest.votes.filter((vote) => vote.wasChangedByRules).map((vote) => `<tr>
    <td>${escapeHtml(vote.voterName)}</td>
    <td>${numberText(vote.workNo)}</td>
    <td>${escapeHtml(vote.originalScoreText)}</td>
    <td>${escapeHtml(vote.scoreText)}</td>
    <td>${escapeHtml(vote.ruleNote)}</td>
  </tr>`).join("");
}

function renderPeople() {
  const contest = currentContest();
  const people = new Map();
  const touch = (name) => {
    const key = normalizeName(name);
    if (!key) return null;
    if (!people.has(key)) people.set(key, { name, author: false, voter: false, works: 0, votes: 0 });
    return people.get(key);
  };
  for (const work of contest.works) {
    if (!work.author || sameName(work.author, UNKNOWN_AUTHOR)) continue;
    const row = touch(work.author);
    if (row) {
      row.author = true;
      row.works++;
    }
  }
  for (const vote of contest.votes) {
    const row = touch(vote.voterName);
    if (row) {
      row.voter = true;
      row.votes++;
    }
  }
  $("peopleRows").innerHTML = [...people.values()].sort((a, b) => a.name.localeCompare(b.name, "ru")).map((row) => `<tr>
    <td>${escapeHtml(row.name)}</td>
    <td>${row.author ? "да" : ""}</td>
    <td>${row.voter ? "да" : ""}</td>
    <td>${row.works}</td>
    <td>${row.votes}</td>
  </tr>`).join("");
}

function saveSettingsFromForm() {
  const contest = currentContest();
  Object.assign(contest, {
    number: $("contestNumber").value.trim() || "без №",
    name: $("contestName").value.trim() || "Новый конкурс",
    hostName: $("hostName").value.trim(),
    nextHostName: $("nextHostName").value.trim(),
    voteLimit: Number($("voteLimit").value || 0),
    baseVote: Number($("baseVote").value || 3),
    maxVote: Number($("maxVote").value || 4),
    limitMaxVote: Number($("limitMaxVote").value || 0),
    hostKnowsAuthors: $("hostKnowsAuthors").checked,
    limitMaxVoteByTopic: $("limitMaxVoteByTopic").checked,
    oneMaxVotePerTopic: $("oneMaxVotePerTopic").checked,
    downgradeExtraMaxVote: $("downgradeExtraMaxVote").checked,
    allowZeroVotes: $("allowZeroVotes").checked,
    treatSelfVoteAsZero: $("selfVoteZero").checked,
    updatedAt: new Date().toISOString()
  });
  saveState();
  renderAll();
  toast("Настройки сохранены.");
}

function exportCsv(filename, rows) {
  const csv = rows.map((row) => row.map((cell) => `"${String(cell ?? "").replace(/"/g, '""')}"`).join(";")).join("\r\n");
  download(filename, csv, "text/csv;charset=utf-8");
}

function download(filename, content, type) {
  const blob = new Blob([content], { type });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}

function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>"']/g, (ch) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[ch]));
}

function toast(message) {
  const node = $("toast");
  node.textContent = message;
  node.classList.add("show");
  clearTimeout(toast.timer);
  toast.timer = setTimeout(() => node.classList.remove("show"), 2200);
}

viewButtons.forEach((button) => {
  button.addEventListener("click", () => {
    viewButtons.forEach((item) => item.classList.toggle("active", item === button));
    document.querySelectorAll(".view").forEach((view) => view.classList.remove("active"));
    $(`view-${button.dataset.view}`).classList.add("active");
  });
});

$("contestSelect").addEventListener("change", (event) => {
  currentContestId = event.target.value;
  lastPreview = { blocks: [], warnings: [] };
  saveState();
  renderAll();
});

$("contestRows").addEventListener("click", (event) => {
  const row = event.target.closest("[data-contest-id]");
  if (!row) return;
  currentContestId = row.dataset.contestId;
  saveState();
  renderAll();
});

$("newContestBtn").addEventListener("click", () => {
  const contest = defaultContest();
  contest.number = String(state.contests.length + 1).padStart(3, "0");
  state.contests.push(contest);
  currentContestId = contest.id;
  saveState();
  renderAll();
  toast("Конкурс создан.");
});

$("duplicateContestBtn").addEventListener("click", () => {
  const copy = JSON.parse(JSON.stringify(currentContest()));
  copy.id = uid();
  copy.name = `${copy.name} - копия`;
  copy.votes = [];
  copy.createdAt = new Date().toISOString();
  copy.updatedAt = copy.createdAt;
  state.contests.push(copy);
  currentContestId = copy.id;
  saveState();
  renderAll();
  toast("Копия создана без голосов.");
});

$("deleteContestBtn").addEventListener("click", () => {
  if (state.contests.length <= 1) {
    toast("Нельзя удалить единственный конкурс.");
    return;
  }
  if (!confirm("Удалить текущий конкурс?")) return;
  state.contests = state.contests.filter((contest) => contest.id !== currentContestId);
  currentContestId = state.contests[0].id;
  saveState();
  renderAll();
});

$("saveSettingsBtn").addEventListener("click", saveSettingsFromForm);

$("importWorksBtn").addEventListener("click", () => {
  saveSettingsFromForm();
  const contest = currentContest();
  const mode = contest.hostKnowsAuthors ? "known" : "hidden";
  const result = parseWorks($("worksText").value, mode);
  if (!result.works.length) {
    toast("Работы не найдены.");
    return;
  }
  contest.works = result.works;
  contest.updatedAt = new Date().toISOString();
  saveState();
  renderAll();
  toast(`Импортировано работ: ${result.works.length}.`);
});

$("applyAuthorsBtn").addEventListener("click", () => {
  const contest = currentContest();
  const result = parseWorks($("worksText").value, "authors");
  const authors = new Map(result.works.map((work) => [Number(work.number), work.author]));
  for (const work of contest.works) {
    const author = authors.get(Number(work.number));
    if (author) work.author = author;
  }
  contest.updatedAt = new Date().toISOString();
  saveState();
  renderAll();
  toast("Авторы применены.");
});

$("parseVotesBtn").addEventListener("click", () => {
  lastPreview = parseVotes($("votesText").value, currentContest());
  renderPreview(lastPreview);
  toast(`Найдено блоков: ${lastPreview.blocks.length}.`);
});

$("acceptVotesBtn").addEventListener("click", () => {
  if (!lastPreview.blocks.length) lastPreview = parseVotes($("votesText").value, currentContest());
  const contest = currentContest();
  const incoming = lastPreview.blocks.flatMap((block) => block.votes);
  const keys = new Set(incoming.map((vote) => `${vote.voterKey}|${vote.workNo}`));
  contest.votes = contest.votes.filter((vote) => !keys.has(`${vote.voterKey}|${vote.workNo}`)).concat(incoming);
  contest.updatedAt = new Date().toISOString();
  saveState();
  renderAll();
  toast(`Принято оценок: ${incoming.length}.`);
});

$("clearVoteTextBtn").addEventListener("click", () => {
  $("votesText").value = "";
  lastPreview = { blocks: [], warnings: [] };
  renderPreview(lastPreview);
});

$("backupBtn").addEventListener("click", () => {
  download(`votecounter-html-backup-${new Date().toISOString().slice(0, 10)}.json`, JSON.stringify(state, null, 2), "application/json;charset=utf-8");
});

$("restoreInput").addEventListener("change", async (event) => {
  const file = event.target.files[0];
  if (!file) return;
  const parsed = JSON.parse(await file.text());
  if (!Array.isArray(parsed.contests) || !parsed.contests.length) {
    toast("В файле нет конкурсов.");
    return;
  }
  state.contests = parsed.contests;
  currentContestId = parsed.currentContestId || state.contests[0].id;
  saveState();
  renderAll();
  toast("Резервная копия загружена.");
});

$("copyFinalBtn").addEventListener("click", async () => {
  const text = $("finalText").value;
  if (navigator.clipboard && window.isSecureContext) {
    await navigator.clipboard.writeText(text);
  } else {
    $("finalText").focus();
    $("finalText").select();
    document.execCommand("copy");
  }
  toast("Итоги скопированы.");
});

$("downloadFinalBtn").addEventListener("click", () => {
  const contest = currentContest();
  const html = `<!doctype html><html lang="ru"><meta charset="utf-8"><title>${escapeHtml(contest.name)}</title><body><pre>${escapeHtml($("finalText").value)}</pre></body></html>`;
  download(`results-${contest.number || "contest"}.html`, html, "text/html;charset=utf-8");
});

$("exportRatingCsvBtn").addEventListener("click", () => {
  const rows = [["Место", "Номер", "Название", "Автор", "Баллы", "Голосов", "Средняя"]];
  for (const row of buildResults(currentContest()).rows) rows.push([placeText(row), numberText(row.workNo), row.title, row.author, row.rate, row.acceptedVotes, row.average]);
  exportCsv("rating.csv", rows);
});

$("exportControlCsvBtn").addEventListener("click", () => {
  const rows = [["Судья", "Работа", "Оценка", "Принято", "Правило"]];
  for (const vote of currentContest().votes) rows.push([vote.voterName, numberText(vote.workNo), vote.originalScoreText, vote.scoreText, vote.ruleNote]);
  exportCsv("votes-control.csv", rows);
});

$("exportPeopleCsvBtn").addEventListener("click", () => {
  const rows = [["Имя", "Автор работ", "Голосующий", "Работы", "Оценок"]];
  [...$("peopleRows").querySelectorAll("tr")].forEach((tr) => rows.push([...tr.children].map((td) => td.textContent)));
  exportCsv("people.csv", rows);
});

renderAll();
