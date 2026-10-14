CREATE TABLE integration_read_state (
  profile_id TEXT NOT NULL,
  connection_id TEXT NOT NULL,
  blocked_until_utc TEXT,
  credential_repair_required INTEGER NOT NULL DEFAULT 0,
  PRIMARY KEY (profile_id, connection_id)
);
