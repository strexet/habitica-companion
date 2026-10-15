ALTER TABLE workspaces ADD COLUMN authority_json TEXT;
CREATE TABLE workspace_runtime (
 profile_id TEXT PRIMARY KEY REFERENCES workspaces(profile_id) ON DELETE CASCADE,
 access_epoch TEXT NOT NULL,
 lease_token TEXT,
 lease_until_utc TEXT,
 next_network_at_utc TEXT,
 last_wake_at_utc TEXT,
 command_id TEXT,
 prepared_request_json TEXT
);
CREATE TABLE workspace_handoffs (
 profile_id TEXT NOT NULL REFERENCES workspaces(profile_id) ON DELETE CASCADE,
 transfer_id TEXT NOT NULL,
 archive_hash TEXT NOT NULL,
 workspace_id TEXT NOT NULL,
 accepted_at_utc TEXT NOT NULL,
 PRIMARY KEY(profile_id,transfer_id)
);
