CREATE TABLE workspace_preparations (
 profile_id TEXT NOT NULL REFERENCES workspaces(profile_id) ON DELETE CASCADE,
 request_id TEXT NOT NULL,
 access_epoch TEXT NOT NULL,
 command_id TEXT NOT NULL,
 input_hash TEXT NOT NULL,
 command_json TEXT NOT NULL,
 correlation_id TEXT NOT NULL,
 created_at_utc TEXT NOT NULL,
 expires_at_utc TEXT NOT NULL,
 PRIMARY KEY(profile_id,request_id),
 UNIQUE(profile_id,command_id)
);
