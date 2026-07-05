ALTER TABLE contest_comments ADD COLUMN is_deleted INTEGER NOT NULL DEFAULT 0;
ALTER TABLE contest_comments ADD COLUMN deleted_at TEXT;
ALTER TABLE contest_comments ADD COLUMN deleted_by TEXT;

CREATE INDEX IF NOT EXISTS idx_contest_comments_is_deleted ON contest_comments(is_deleted);
