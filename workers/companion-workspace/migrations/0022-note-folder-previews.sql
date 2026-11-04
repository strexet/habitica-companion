CREATE TABLE note_folder_preparations (
 profile_id TEXT NOT NULL REFERENCES notes_notebooks(profile_id) ON DELETE CASCADE,
 request_id TEXT NOT NULL,
 command_id TEXT NOT NULL,
 notebook_id TEXT NOT NULL,
 folder_id TEXT NOT NULL,
 access_epoch TEXT NOT NULL,
 grant_epoch TEXT NOT NULL,
 input_hash TEXT NOT NULL,
 command_json TEXT,
 created_at_utc TEXT NOT NULL,
 expires_at_utc TEXT NOT NULL,
 PRIMARY KEY(profile_id,request_id),
 UNIQUE(profile_id,command_id)
);
CREATE INDEX note_folder_preparations_expiry ON note_folder_preparations(expires_at_utc) WHERE command_json IS NOT NULL;
