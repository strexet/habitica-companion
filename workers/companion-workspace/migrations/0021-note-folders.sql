ALTER TABLE notes_notebooks ADD COLUMN schema_version INTEGER NOT NULL DEFAULT 1 CHECK(schema_version IN (1,2));
ALTER TABLE personal_notes ADD COLUMN folder_json TEXT;
CREATE TABLE note_folders (
 profile_id TEXT NOT NULL REFERENCES notes_notebooks(profile_id) ON DELETE CASCADE,
 folder_id TEXT NOT NULL,
 folder_json TEXT NOT NULL,
 PRIMARY KEY(profile_id,folder_id)
);
CREATE TABLE note_folder_receipts (
 profile_id TEXT NOT NULL REFERENCES notes_notebooks(profile_id) ON DELETE CASCADE,
 command_id TEXT NOT NULL,
 sequence INTEGER NOT NULL,
 receipt_json TEXT NOT NULL,
 PRIMARY KEY(profile_id,command_id),
 UNIQUE(profile_id,sequence)
);
