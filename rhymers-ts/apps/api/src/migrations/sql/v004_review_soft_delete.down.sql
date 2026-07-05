DROP INDEX IF EXISTS idx_work_reviews_is_deleted;

ALTER TABLE work_reviews RENAME TO work_reviews_old;

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
  created_at TEXT NOT NULL
);

INSERT INTO work_reviews (
  id,
  contest_id,
  work_number,
  work_title,
  reviewer_name,
  reviewer_role,
  title,
  content,
  rating,
  strengths,
  areas_for_improvement,
  author_response,
  helpful_count,
  is_approved,
  is_hidden,
  created_at
)
SELECT
  id,
  contest_id,
  work_number,
  work_title,
  reviewer_name,
  reviewer_role,
  title,
  content,
  rating,
  strengths,
  areas_for_improvement,
  author_response,
  helpful_count,
  is_approved,
  is_hidden,
  created_at
FROM work_reviews_old;

DROP TABLE work_reviews_old;

CREATE INDEX IF NOT EXISTS idx_work_reviews_contest_work ON work_reviews(contest_id, work_number);
