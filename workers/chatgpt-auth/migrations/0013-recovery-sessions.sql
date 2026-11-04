CREATE TABLE integration_recovery_sessions (
  token_hash TEXT PRIMARY KEY,
  profile_id TEXT NOT NULL REFERENCES integration_profiles(profile_id) ON DELETE CASCADE,
  connection_id TEXT NOT NULL,
  management_anchor_hash TEXT NOT NULL,
  created_at_utc TEXT NOT NULL,
  expires_at_utc TEXT NOT NULL
);
CREATE INDEX integration_recovery_sessions_profile ON integration_recovery_sessions(profile_id, created_at_utc);
