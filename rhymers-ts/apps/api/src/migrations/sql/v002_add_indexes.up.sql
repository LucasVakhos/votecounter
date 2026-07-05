CREATE INDEX IF NOT EXISTS idx_contest_works_contest_id ON contest_works(contest_id);
CREATE INDEX IF NOT EXISTS idx_votes_contest_id ON votes(contest_id);
CREATE INDEX IF NOT EXISTS idx_contest_comments_contest_id ON contest_comments(contest_id);
CREATE INDEX IF NOT EXISTS idx_work_reviews_contest_work ON work_reviews(contest_id, work_number);
CREATE INDEX IF NOT EXISTS idx_sorrow_messages_contest_id ON sorrow_messages(contest_id);
