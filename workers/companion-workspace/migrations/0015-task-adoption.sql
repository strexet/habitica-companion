CREATE TABLE workspace_task_adoptions (
 profile_id TEXT NOT NULL REFERENCES workspaces(profile_id) ON DELETE CASCADE,
 command_id TEXT NOT NULL, request_id TEXT NOT NULL, input_hash TEXT NOT NULL,
 workspace_id TEXT NOT NULL, connection_id TEXT NOT NULL, access_epoch TEXT NOT NULL,
 grant_epoch TEXT NOT NULL, origin TEXT NOT NULL,
 expected_revision INTEGER NOT NULL, review_hash TEXT NOT NULL, review_json TEXT NOT NULL,
 state TEXT NOT NULL CHECK(state IN ('prepared','accepted','refused')),
 receipt_json TEXT, error_code TEXT, created_at_utc TEXT NOT NULL, expires_at_utc TEXT NOT NULL,
 PRIMARY KEY(profile_id,command_id), UNIQUE(profile_id,request_id)
);
CREATE TRIGGER workspace_task_adoption_size AFTER INSERT ON workspace_task_adoptions
 BEGIN UPDATE workspaces SET archive_bytes=archive_bytes+length(CAST(NEW.review_json AS BLOB))+1024,store_version=store_version+1 WHERE profile_id=NEW.profile_id; END;
