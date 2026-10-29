ALTER TABLE workspace_preparations ADD COLUMN tag_authority_json TEXT;
CREATE TABLE workspace_tag_work (
 profile_id TEXT NOT NULL REFERENCES workspaces(profile_id) ON DELETE CASCADE,
 work_id TEXT NOT NULL,
 workspace_id TEXT NOT NULL,
 connection_id TEXT NOT NULL,
 access_epoch TEXT NOT NULL,
 command_id TEXT NOT NULL,
 source_revision INTEGER NOT NULL,
 intent_id TEXT NOT NULL,
 desired_hash TEXT NOT NULL,
 work_json TEXT NOT NULL,
 prepared_request_json TEXT,
 state TEXT NOT NULL,
 error_code TEXT,
 retry_at_utc TEXT,
 created_at_utc TEXT NOT NULL,
 updated_at_utc TEXT NOT NULL,
 PRIMARY KEY(profile_id,work_id),
 UNIQUE(profile_id,intent_id)
);
CREATE INDEX workspace_tag_work_pending ON workspace_tag_work(profile_id,state,created_at_utc);
CREATE TABLE workspace_tag_status (
 profile_id TEXT PRIMARY KEY REFERENCES workspaces(profile_id) ON DELETE CASCADE,
 source_revision INTEGER NOT NULL,
 gap TEXT
);
