CREATE TABLE workspace_linked_recoveries (
 profile_id TEXT NOT NULL REFERENCES workspaces(profile_id) ON DELETE CASCADE,
 command_id TEXT NOT NULL, request_id TEXT NOT NULL, input_hash TEXT NOT NULL,
 connection_id TEXT NOT NULL, access_epoch TEXT NOT NULL, grant_epoch TEXT NOT NULL,
 account_id TEXT NOT NULL, plan_json TEXT NOT NULL, review_json TEXT NOT NULL,
 check_token TEXT NOT NULL, state TEXT NOT NULL, prepared_request_json TEXT,
 native_outcome_json TEXT, receipt_json TEXT, error_code TEXT,
 created_at_utc TEXT NOT NULL, expires_at_utc TEXT NOT NULL,
 PRIMARY KEY(profile_id,command_id), UNIQUE(profile_id,request_id)
);
ALTER TABLE workspaces ADD COLUMN linked_recovery_fence TEXT;
CREATE TRIGGER workspace_linked_recovery_fence BEFORE UPDATE OF document_json ON workspaces
 WHEN OLD.linked_recovery_fence IS NOT NULL AND NEW.document_json<>OLD.document_json AND NEW.commit_tag<>OLD.linked_recovery_fence
 BEGIN SELECT RAISE(ABORT,'linked_recovery_pending'); END;
