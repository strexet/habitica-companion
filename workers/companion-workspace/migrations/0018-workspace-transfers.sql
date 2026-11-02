-- Immutable source archives; migration never enters the executable command journal.
CREATE TABLE workspace_transfers (
 profile_id TEXT NOT NULL REFERENCES workspaces(profile_id) ON DELETE CASCADE,
 transfer_id TEXT NOT NULL,
 artifact_hash TEXT NOT NULL,
 artifact_bytes INTEGER NOT NULL,
 manifest_json TEXT NOT NULL,
 access_epoch TEXT NOT NULL,
 notes_epoch TEXT,
 state TEXT NOT NULL CHECK(state IN ('staged','prepared','accepted')),
 review_json TEXT,
 review_hash TEXT,
 expires_at_utc TEXT,
 receipt_json TEXT,
 created_at_utc TEXT NOT NULL,
 PRIMARY KEY(profile_id,transfer_id)
);
CREATE TABLE workspace_transfer_chunks (
 profile_id TEXT NOT NULL,
 transfer_id TEXT NOT NULL,
 part TEXT NOT NULL,
 sequence INTEGER NOT NULL,
 chunk TEXT NOT NULL,
 PRIMARY KEY(profile_id,transfer_id,part,sequence),
 FOREIGN KEY(profile_id,transfer_id) REFERENCES workspace_transfers(profile_id,transfer_id) ON DELETE CASCADE
);
CREATE TABLE workspace_transfer_fences (
 profile_id TEXT NOT NULL REFERENCES workspaces(profile_id) ON DELETE CASCADE,
 task_id TEXT NOT NULL,
 transfer_id TEXT NOT NULL,
 PRIMARY KEY(profile_id,task_id)
);
CREATE TABLE notes_transfer_markers (
 profile_id TEXT NOT NULL REFERENCES notes_notebooks(profile_id) ON DELETE CASCADE,
 transfer_id TEXT NOT NULL,
 notebook_revision INTEGER NOT NULL,
 marker_json TEXT NOT NULL,
 PRIMARY KEY(profile_id,transfer_id),
 UNIQUE(profile_id,notebook_revision)
);
