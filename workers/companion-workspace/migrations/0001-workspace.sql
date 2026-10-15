CREATE TABLE workspaces (
 profile_id TEXT PRIMARY KEY,
 workspace_id TEXT NOT NULL UNIQUE,
 connection_id TEXT NOT NULL,
 access_epoch TEXT NOT NULL,
 consent_version TEXT NOT NULL,
 consented_at_utc TEXT NOT NULL,
 active INTEGER NOT NULL DEFAULT 1 CHECK(active IN (0,1)),
 executor_enabled INTEGER NOT NULL DEFAULT 0 CHECK(executor_enabled IN (0,1)),
 document_json TEXT NOT NULL,
 store_version INTEGER NOT NULL DEFAULT 1,
 archive_bytes INTEGER NOT NULL,
 commit_tag TEXT NOT NULL,
 created_at_utc TEXT NOT NULL,
 updated_at_utc TEXT NOT NULL
);
CREATE TABLE workspace_sessions (
 token_hash TEXT PRIMARY KEY,
 profile_id TEXT NOT NULL REFERENCES workspaces(profile_id) ON DELETE CASCADE,
 connection_id TEXT NOT NULL,
 access_epoch TEXT NOT NULL,
 created_at_utc TEXT NOT NULL,
 expires_at_utc TEXT NOT NULL
);
CREATE INDEX workspace_sessions_profile ON workspace_sessions(profile_id,created_at_utc);
CREATE TABLE workspace_commands (
 profile_id TEXT NOT NULL REFERENCES workspaces(profile_id) ON DELETE CASCADE,
 command_id TEXT NOT NULL,
 sequence INTEGER NOT NULL,
 payload_hash TEXT NOT NULL,
 entry_json TEXT NOT NULL,
 PRIMARY KEY(profile_id,command_id),
 UNIQUE(profile_id,sequence)
);
CREATE TABLE workspace_events (
 profile_id TEXT NOT NULL REFERENCES workspaces(profile_id) ON DELETE CASCADE,
 event_id TEXT NOT NULL,
 sequence INTEGER NOT NULL,
 event_json TEXT NOT NULL,
 PRIMARY KEY(profile_id,event_id),
 UNIQUE(profile_id,sequence)
);
