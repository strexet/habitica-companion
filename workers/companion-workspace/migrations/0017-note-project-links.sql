-- Optional current association; retained versions remain in existing version_json.
ALTER TABLE personal_notes ADD COLUMN project_json TEXT;
