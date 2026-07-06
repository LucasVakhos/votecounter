DROP INDEX IF EXISTS idx_moderation_actions_performed_at;
DROP INDEX IF EXISTS idx_moderation_actions_moderator;
DROP INDEX IF EXISTS idx_moderation_actions_target;
DROP TABLE IF EXISTS moderation_actions;

-- SQLite does not support DROP COLUMN in all versions; rebuild comments table
ALTER TABLE contest_comments RENAME TO contest_comments_v5;

CREATE TABLE contest_comments (
  id TEXT PRIMARY KEY,
  contest_id TEXT NOT NULL,
  author_name TEXT NOT NULL,
  author_role TEXT NOT NULL,
  content TEXT NOT NULL,
  parent_comment_id TEXT,
  likes_count INTEGER NOT NULL,
  is_approved INTEGER NOT NULL,
  is_hidden INTEGER NOT NULL,
  is_deleted INTEGER NOT NULL DEFAULT 0,
  deleted_at TEXT,
  deleted_by TEXT,
  created_at TEXT NOT NULL
);

INSERT INTO contest_comments SELECT id,contest_id,author_name,author_role,content,parent_comment_id,likes_count,is_approved,is_hidden,is_deleted,deleted_at,deleted_by,created_at FROM contest_comments_v5;
DROP TABLE contest_comments_v5;

CREATE INDEX IF NOT EXISTS idx_contest_comments_contest_id ON contest_comments(contest_id);
CREATE INDEX IF NOT EXISTS idx_contest_comments_is_deleted ON contest_comments(is_deleted);

-- Rebuild work_reviews table to remove delete_reason column
ALTER TABLE work_reviews RENAME TO work_reviews_v5;

CREATE TABLE work_reviews (
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
  is_deleted INTEGER NOT NULL DEFAULT 0,
  deleted_at TEXT,
  deleted_by TEXT,
  created_at TEXT NOT NULL
);

INSERT INTO work_reviews SELECT id,contest_id,work_number,work_title,reviewer_name,reviewer_role,title,content,rating,strengths,areas_for_improvement,author_response,helpful_count,is_approved,is_hidden,is_deleted,deleted_at,deleted_by,created_at FROM work_reviews_v5;
DROP TABLE work_reviews_v5;

CREATE INDEX IF NOT EXISTS idx_work_reviews_contest_work ON work_reviews(contest_id, work_number);
CREATE INDEX IF NOT EXISTS idx_work_reviews_is_deleted ON work_reviews(is_deleted);
