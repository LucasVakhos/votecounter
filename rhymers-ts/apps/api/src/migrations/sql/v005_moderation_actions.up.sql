CREATE TABLE IF NOT EXISTS moderation_actions (
  id TEXT PRIMARY KEY,
  moderator_name TEXT NOT NULL,
  action TEXT NOT NULL,
  target_type TEXT NOT NULL,
  target_id TEXT NOT NULL,
  reason TEXT,
  performed_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_moderation_actions_target ON moderation_actions(target_type, target_id);
CREATE INDEX IF NOT EXISTS idx_moderation_actions_moderator ON moderation_actions(moderator_name);
CREATE INDEX IF NOT EXISTS idx_moderation_actions_performed_at ON moderation_actions(performed_at);

ALTER TABLE contest_comments ADD COLUMN delete_reason TEXT;
ALTER TABLE work_reviews ADD COLUMN delete_reason TEXT;
