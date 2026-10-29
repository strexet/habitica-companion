CREATE TABLE diagnostic_profiles (
 profile_id TEXT PRIMARY KEY REFERENCES workspaces(profile_id) ON DELETE CASCADE,
 connection_id TEXT NOT NULL,workspace_access_epoch TEXT NOT NULL,grant_epoch TEXT NOT NULL,
 consent_epoch TEXT NOT NULL,active INTEGER NOT NULL DEFAULT 0,revision INTEGER NOT NULL DEFAULT 1,
 event_count INTEGER NOT NULL DEFAULT 0,total_bytes INTEGER NOT NULL DEFAULT 0,
 cleanup_pending INTEGER NOT NULL DEFAULT 0,cleanup_attempt_at_utc TEXT,commit_tag TEXT NOT NULL
);
CREATE TABLE diagnostic_uploads (
 profile_id TEXT NOT NULL REFERENCES diagnostic_profiles(profile_id) ON DELETE CASCADE,
 upload_id TEXT NOT NULL,request_id TEXT NOT NULL,bundle_hash TEXT NOT NULL,event_count INTEGER NOT NULL,payload_bytes INTEGER NOT NULL,
 consent_epoch TEXT NOT NULL,state TEXT NOT NULL DEFAULT 'prepared',payload_json TEXT,receipt_json TEXT,
 created_at_utc TEXT NOT NULL,accept_by_utc TEXT NOT NULL,expires_at_utc TEXT NOT NULL,
 PRIMARY KEY(profile_id,upload_id),UNIQUE(profile_id,request_id)
);
CREATE INDEX diagnostic_uploads_expiry ON diagnostic_uploads(expires_at_utc);
CREATE TABLE diagnostic_events (
 profile_id TEXT NOT NULL,upload_id TEXT NOT NULL,event_id TEXT NOT NULL,ordinal INTEGER NOT NULL,
 occurred_at_utc TEXT NOT NULL,feature TEXT NOT NULL,operation TEXT NOT NULL,severity TEXT NOT NULL,
 command_id TEXT,correlation_id TEXT,entry_json TEXT NOT NULL,
 PRIMARY KEY(profile_id,event_id),FOREIGN KEY(profile_id,upload_id) REFERENCES diagnostic_uploads(profile_id,upload_id) ON DELETE CASCADE
);
