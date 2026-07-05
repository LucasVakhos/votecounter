ALTER TABLE work_reviews ADD COLUMN is_deleted INTEGER NOT NULL DEFAULT 0;
ALTER TABLE work_reviews ADD COLUMN deleted_at TEXT;
ALTER TABLE work_reviews ADD COLUMN deleted_by TEXT;

CREATE INDEX IF NOT EXISTS idx_work_reviews_is_deleted ON work_reviews(is_deleted);
