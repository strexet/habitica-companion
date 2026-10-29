CREATE TABLE notes_notebooks (
 profile_id TEXT PRIMARY KEY,
 notebook_id TEXT NOT NULL UNIQUE,
 owner_scope TEXT NOT NULL,
 connection_id TEXT NOT NULL,
 access_epoch TEXT NOT NULL,
 consent_version TEXT NOT NULL,
 active INTEGER NOT NULL CHECK(active IN (0,1)),
 revision INTEGER NOT NULL DEFAULT 0,
 commit_tag TEXT NOT NULL,
 consented_at_utc TEXT NOT NULL
);
CREATE TABLE notes_sessions (
 token_hash TEXT PRIMARY KEY,
 profile_id TEXT NOT NULL REFERENCES notes_notebooks(profile_id) ON DELETE CASCADE,
 connection_id TEXT NOT NULL,
 access_epoch TEXT NOT NULL,
 created_at_utc TEXT NOT NULL,
 expires_at_utc TEXT NOT NULL
);
CREATE INDEX notes_sessions_profile ON notes_sessions(profile_id);
CREATE TABLE personal_notes (
 profile_id TEXT NOT NULL REFERENCES notes_notebooks(profile_id) ON DELETE CASCADE,
 note_id TEXT NOT NULL,
 created_at_utc TEXT NOT NULL,
 revision INTEGER NOT NULL,
 title TEXT,
 markdown TEXT,
 is_trashed INTEGER NOT NULL CHECK(is_trashed IN (0,1)),
 is_purged INTEGER NOT NULL DEFAULT 0 CHECK(is_purged IN (0,1)),
 at_utc TEXT NOT NULL,
 PRIMARY KEY(profile_id,note_id)
);
CREATE TABLE personal_note_versions (
 profile_id TEXT NOT NULL,
 note_id TEXT NOT NULL,
 revision INTEGER NOT NULL,
 version_json TEXT NOT NULL,
 PRIMARY KEY(profile_id,note_id,revision),
 FOREIGN KEY(profile_id,note_id) REFERENCES personal_notes(profile_id,note_id) ON DELETE CASCADE
);
CREATE TABLE personal_note_receipts (
 profile_id TEXT NOT NULL REFERENCES notes_notebooks(profile_id) ON DELETE CASCADE,
 command_id TEXT NOT NULL,
 sequence INTEGER NOT NULL,
 receipt_json TEXT NOT NULL,
 PRIMARY KEY(profile_id,command_id),
 UNIQUE(profile_id,sequence)
);
