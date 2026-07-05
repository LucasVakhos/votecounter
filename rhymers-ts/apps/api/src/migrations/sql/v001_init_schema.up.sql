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
