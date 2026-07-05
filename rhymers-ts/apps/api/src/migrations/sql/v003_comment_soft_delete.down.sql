DROP INDEX IF EXISTS idx_contest_comments_is_deleted;

ALTER TABLE contest_comments RENAME TO contest_comments_old;

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
  created_at TEXT NOT NULL
);

INSERT INTO contest_comments (
  id,
  contest_id,
  author_name,
  author_role,
  content,
  parent_comment_id,
  likes_count,
  is_approved,
  is_hidden,
  created_at
)
SELECT
  id,
  contest_id,
  author_name,
  author_role,
  content,
  parent_comment_id,
  likes_count,
  is_approved,
  is_hidden,
  created_at
FROM contest_comments_old;

DROP TABLE contest_comments_old;

CREATE INDEX IF NOT EXISTS idx_contest_comments_contest_id ON contest_comments(contest_id);
