CREATE TABLE notes_preparations (
 profile_id TEXT NOT NULL REFERENCES notes_notebooks(profile_id) ON DELETE CASCADE,
 request_id TEXT NOT NULL,
 command_id TEXT NOT NULL,
 notebook_id TEXT NOT NULL,
 note_id TEXT NOT NULL,
 access_epoch TEXT NOT NULL,
 grant_epoch TEXT NOT NULL,
 input_hash TEXT NOT NULL,
 command_json TEXT,
 created_at_utc TEXT NOT NULL,
 expires_at_utc TEXT NOT NULL,
 PRIMARY KEY(profile_id,request_id),
 UNIQUE(profile_id,command_id)
);
CREATE INDEX notes_preparations_note ON notes_preparations(profile_id,note_id);
CREATE INDEX notes_preparations_expiry ON notes_preparations(expires_at_utc) WHERE command_json IS NOT NULL;
